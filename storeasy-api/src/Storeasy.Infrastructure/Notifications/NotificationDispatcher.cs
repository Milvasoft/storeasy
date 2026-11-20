using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Notifications;
using Storeasy.Domain.Organizations;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="INotificationDispatcher"/>: resolves the recipients (organization roles, named users, platform
/// admins), applies each user's preference for the kind, writes the in-app notifications in one insert, pushes
/// <c>notification.created</c> to each recipient, queues the e-mails (outbox) and the web pushes, and queues the outgoing
/// webhook and the live event when the notification names them. Custom role members receive what the seeded role their
/// permissions match (<see cref="OrganizationRole.AudienceRole"/>) receives.
/// <para>
/// A notification about an app whose alerts are muted (<see cref="App.AlertsMutedDate"/>) reaches no role audience and
/// publishes no live event; users it names directly (the member whose own action failed) still get it, and the outgoing
/// webhook is still queued, since integrations subscribe to events rather than to alerts.
/// </para>
/// </summary>
/// <param name="appRepository"></param>
/// <param name="memberRepository"></param>
/// <param name="userRepository"></param>
/// <param name="preferenceRepository"></param>
/// <param name="notificationRepository"></param>
/// <param name="emailOutbox"></param>
/// <param name="webPushQueue"></param>
/// <param name="localizer"></param>
/// <param name="userNotificationPublisher"></param>
/// <param name="activityPublisher"></param>
/// <param name="orgWebhookPublisher"></param>
/// <param name="appOptions"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class NotificationDispatcher(IStoreasyRepositoryBase<App> appRepository,
                                           IStoreasyRepositoryBase<OrganizationMember> memberRepository,
                                           IStoreasyRepositoryBase<User> userRepository,
                                           IStoreasyRepositoryBase<NotificationPreference> preferenceRepository,
                                           IStoreasyRepositoryBase<InternalNotification> notificationRepository,
                                           IEmailOutbox emailOutbox,
                                           IWebPushQueue webPushQueue,
                                           IMilvaLocalizer localizer,
                                           IUserNotificationPublisher userNotificationPublisher,
                                           IActivityPublisher activityPublisher,
                                           IOrgWebhookPublisher orgWebhookPublisher,
                                           AppOptions appOptions,
                                           TimeProvider timeProvider,
                                           IMilvaLogger logger) : INotificationDispatcher
{
    private const int _textMaxLength = 1000;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = userRepository;
    private readonly IStoreasyRepositoryBase<NotificationPreference> _preferenceRepository = preferenceRepository;
    private readonly IStoreasyRepositoryBase<InternalNotification> _notificationRepository = notificationRepository;
    private readonly IEmailOutbox _emailOutbox = emailOutbox;
    private readonly IWebPushQueue _webPushQueue = webPushQueue;
    private readonly IMilvaLocalizer _localizer = localizer;
    private readonly IUserNotificationPublisher _userNotificationPublisher = userNotificationPublisher;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly IOrgWebhookPublisher _orgWebhookPublisher = orgWebhookPublisher;
    private readonly AppOptions _appOptions = appOptions;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<int> DispatchAsync(OrgNotification notification, CancellationToken cancellationToken)
    {
        var delivered = 0;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        string text = notification.TextKey == null ? null : (string)_localizer[notification.TextKey, notification.TextArgs ?? []];

        text = text?.Length > _textMaxLength ? text[.._textMaxLength] : text;

        var alertsMuted = false;

        try
        {
            alertsMuted = await AlertsMutedAsync(notification, cancellationToken);
            delivered = await NotifyUsersAsync(notification, text, now, alertsMuted, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error(ex, "Notification {Kind} could not be delivered: {Error}", notification.Kind, SecretRedactor.Redact(ex.Message));
        }

        if (notification.OrganizationId.HasValue && notification.WebhookEventType != null)
        {
            try
            {
                await _orgWebhookPublisher.EnqueueAsync(notification.OrganizationId.Value, notification.WebhookEventType, notification.WebhookData ?? notification.Data, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error(ex, "Webhook event {EventType} could not be queued: {Error}", notification.WebhookEventType, SecretRedactor.Redact(ex.Message));
            }
        }

        if (notification.OrganizationId.HasValue && notification.ActivityType != null && !alertsMuted)
        {
            await _activityPublisher.PublishAsync(new ActivityEventDto
            {
                Type = notification.ActivityType,
                OrganizationId = notification.OrganizationId.Value,
                AppId = notification.AppId,
                RelatedEntityType = notification.RelatedEntity,
                RelatedEntityId = notification.RelatedEntityId,
                Message = text,
                Date = now,
            }, cancellationToken);
        }

        return delivered;
    }

    private async Task<bool> AlertsMutedAsync(OrgNotification notification, CancellationToken cancellationToken)
    {
        if (!notification.OrganizationId.HasValue || !notification.AppId.HasValue)
            return false;

        var organizationId = notification.OrganizationId.Value;
        var appId = notification.AppId.Value;

        var mutedDate = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == organizationId,
                                                                    projection: a => a.AlertsMutedDate,
                                                                    cancellationToken: cancellationToken);

        return mutedDate != null;
    }

    private async Task<int> NotifyUsersAsync(OrgNotification notification, string text, DateTime now, bool alertsMuted, CancellationToken cancellationToken)
    {
        var recipients = await ResolveRecipientsAsync(notification, alertsMuted, cancellationToken);

        if (recipients.Count == 0)
            return 0;

        var userIds = recipients.Select(r => r.Id).ToList();

        var preferences = (await _preferenceRepository.GetAllAsync(p => userIds.Contains(p.UserId) && p.Kind == notification.Kind,
                                                                   projection: p => new NotificationPreference { UserId = p.UserId, InApp = p.InApp, Email = p.Email, WebPush = p.WebPush },
                                                                   cancellationToken: cancellationToken)).ToDictionary(p => p.UserId);

        var channels = recipients.ToDictionary(r => r.Id, r =>
        {
            var (inApp, email) = preferences.TryGetValue(r.Id, out var row) ? (row.InApp, row.Email) : NotificationKinds.Default(notification.Kind);

            return NotificationKinds.Effective(notification.Kind, inApp, email);
        });

        var data = JsonSerializer.Serialize(new { key = notification.TextKey, args = notification.TextArgs, data = notification.Data }, _json);

        var rows = recipients.Where(r => channels[r.Id].InApp).Select(r => new InternalNotification
        {
            RecipientUserId = r.Id,
            RecipientUserName = r.UserName,
            OrganizationId = notification.OrganizationId,
            Type = notification.Kind,
            Text = text,
            Data = data,
            ActionLink = notification.ActionLink,
            RelatedEntityType = notification.RelatedEntity,
            RelatedEntityId = notification.RelatedEntityId,
            CreationDate = now,
        }).ToList();

        if (rows.Count > 0)
            await _notificationRepository.AddRangeAsync(rows, cancellationToken);

        foreach (var row in rows)
        {
            await _userNotificationPublisher.PublishAsync([row.RecipientUserId], new ActivityEventDto
            {
                Type = ActivityEventTypes.NotificationCreated,
                OrganizationId = notification.OrganizationId ?? 0,
                AppId = notification.AppId,
                NotificationId = row.Id,
                RelatedEntityType = notification.RelatedEntity,
                RelatedEntityId = notification.RelatedEntityId,
                Message = text,
                Date = now,
            }, cancellationToken);
        }

        await SendEmailsAsync(recipients.Where(r => channels[r.Id].Email && r.EmailConfirmed && !string.IsNullOrWhiteSpace(r.Email)).ToList(), text, notification.ActionLink, cancellationToken);

        var pushRecipients = recipients.Where(r => preferences.TryGetValue(r.Id, out var row) ? row.WebPush : NotificationKinds.DefaultWebPush(notification.Kind)).Select(r => r.Id).ToList();

        if (pushRecipients.Count > 0 && !string.IsNullOrWhiteSpace(text))
        {
            var notificationIds = rows.ToDictionary(r => r.RecipientUserId, r => (long?)r.Id);

            _webPushQueue.TryEnqueue(new WebPushMessage(pushRecipients,
                                                        _localizer[$"{nameof(NotificationType)}.{notification.Kind}"],
                                                        text,
                                                        BuildLink(notification.ActionLink),
                                                        notification.Kind.ToString(),
                                                        pushRecipients.Count == 1 ? notificationIds.GetValueOrDefault(pushRecipients[0]) : null));
        }

        return rows.Count;
    }

    private async Task<List<User>> ResolveRecipientsAsync(OrgNotification notification, bool alertsMuted, CancellationToken cancellationToken)
    {
        // A muted app's notification goes only to the users named directly.
        List<OrgRole> roles = alertsMuted ? [] : notification.Roles ?? [];
        var userIds = notification.UserIds ?? [];
        var recipients = new List<User>();

        if (notification.OrganizationId.HasValue && (roles.Count > 0 || userIds.Count > 0))
        {
            var organizationId = notification.OrganizationId.Value;

            var members = await _memberRepository.GetAllAsync(m => m.OrganizationId == organizationId && !m.User.IsDeleted && (roles.Contains(m.Role) || (m.Role == OrgRole.Custom && m.CustomRole != null && roles.Contains(m.CustomRole.AudienceRole)) || userIds.Contains(m.UserId)),
                                                              projection: m => new User { Id = m.User.Id, UserName = m.User.UserName, Email = m.User.Email, EmailConfirmed = m.User.EmailConfirmed, Name = m.User.Name },
                                                              cancellationToken: cancellationToken);

            recipients.AddRange(members);
        }
        else if (!notification.OrganizationId.HasValue && userIds.Count > 0)
        {
            recipients.AddRange(await _userRepository.GetAllAsync(u => userIds.Contains(u.Id) && !u.IsDeleted,
                                                                  projection: u => new User { Id = u.Id, UserName = u.UserName, Email = u.Email, EmailConfirmed = u.EmailConfirmed, Name = u.Name },
                                                                  cancellationToken: cancellationToken));
        }

        if (notification.PlatformAdmins)
        {
            recipients.AddRange(await _userRepository.GetAllAsync(u => u.UserType == UserType.Manager && !u.IsDeleted,
                                                                  projection: u => new User { Id = u.Id, UserName = u.UserName, Email = u.Email, EmailConfirmed = u.EmailConfirmed, Name = u.Name },
                                                                  cancellationToken: cancellationToken));
        }

        return [.. recipients.DistinctBy(r => r.Id)];
    }

    private string BuildLink(string actionLink) => string.IsNullOrWhiteSpace(actionLink) ? _appOptions.BaseUrl : $"{(_appOptions.BaseUrl ?? string.Empty).TrimEnd('/')}/{actionLink.TrimStart('/')}";

    private async Task SendEmailsAsync(List<User> recipients, string text, string actionLink, CancellationToken cancellationToken)
    {
        if (recipients.Count == 0 || string.IsNullOrWhiteSpace(text))
            return;

        var link = BuildLink(actionLink);
        var (subject, html) = NotificationEmailTemplate.Render(_localizer, text, link);

        try
        {
            await _emailOutbox.EnqueueAsync([.. recipients.Select(r => new EmailMessage(r.Email, r.Name, CultureInfo.CurrentUICulture.Name, subject, html, link))], cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Error("Notification e-mails could not be queued: {Error}", SecretRedactor.Redact(ex.Message));
        }
    }
}
