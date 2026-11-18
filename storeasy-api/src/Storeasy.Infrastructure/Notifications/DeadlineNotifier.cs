using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Notifications;
using Storeasy.Domain.Organizations;
using System.Globalization;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IDeadlineNotifier"/>. Each run sends, per subject, only the nearest threshold not sent yet (a build
/// first seen 5 days before expiry gets the 7 day reminder, not the 14 day one). The sent reminders are written before
/// the notifications go out, so two runs never send one twice (unique subject and threshold).
/// </summary>
/// <param name="buildRepository"></param>
/// <param name="reminderRepository"></param>
/// <param name="organizationRepository"></param>
/// <param name="contentService"></param>
/// <param name="readinessService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="msConnectionExpiryReader"></param>
public sealed class DeadlineNotifier(IStoreasyRepositoryBase<Build> buildRepository,
                                     IStoreasyRepositoryBase<DeadlineReminder> reminderRepository,
                                     IStoreasyRepositoryBase<Organization> organizationRepository,
                                     IStoreContentService contentService,
                                     IReadinessService readinessService,
                                     IRulesCatalog rulesCatalog,
                                     INotificationDispatcher notificationDispatcher,
                                     IMsConnectionExpiryReader msConnectionExpiryReader) : IDeadlineNotifier
{
    /// <summary>
    /// TestFlight expiry thresholds in days.
    /// </summary>
    public static readonly int[] BuildThresholds = [14, 7, 1];

    /// <summary>
    /// Compliance deadline thresholds in days.
    /// </summary>
    public static readonly int[] ComplianceThresholds = [30, 7, 1];

    /// <summary>
    /// Microsoft Entra client secret expiry thresholds in days (catalog rule microsoft.api-access.client-secret-lifetime).
    /// </summary>
    public static readonly int[] MsSecretThresholds = [30, 7, 1];

    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<DeadlineReminder> _reminderRepository = reminderRepository;
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = organizationRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IReadinessService _readinessService = readinessService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly IMsConnectionExpiryReader _msConnectionExpiryReader = msConnectionExpiryReader;

    /// <inheritdoc/>
    public async Task<int> NotifyBuildExpiryAsync(DateTime now, CancellationToken cancellationToken)
    {
        var horizon = now.AddDays(BuildThresholds.Max());

        var builds = await _buildRepository.GetAllAsync(b => b.Store == StoreKind.Apple && b.ExpiryDate > now && b.ExpiryDate <= horizon && b.ProcessingState != BuildProcessingState.Expired && !b.StoreApp.App.IsDeleted,
                                                        projection: b => new { b.Id, b.OrganizationId, b.StoreAppId, b.Version, b.BuildNumber, b.ExpiryDate, b.StoreApp.AppId, AppName = b.StoreApp.App.Name },
                                                        cancellationToken: cancellationToken);

        var due = builds.Select(b => new Reminder(b.OrganizationId, $"build:{b.Id}", b.ExpiryDate.Value, new OrgNotification
        {
            OrganizationId = b.OrganizationId,
            Kind = NotificationType.Deadline,
            Roles = NotificationAudiences.Editors,
            TextKey = MessageKey.NotificationBuildExpiring,
            TextArgs = [b.AppName, $"{b.Version} ({b.BuildNumber})", b.ExpiryDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DaysLeft(b.ExpiryDate.Value, now)],
            Data = new { buildId = b.Id, appId = b.AppId, storeAppId = b.StoreAppId, expirationDate = b.ExpiryDate, daysLeft = DaysLeft(b.ExpiryDate.Value, now) },
            AppId = b.AppId,
            RelatedEntity = NotificationEntity.Build,
            RelatedEntityId = b.Id.ToString(CultureInfo.InvariantCulture),
            ActionLink = $"/apps/{b.AppId}/release/testflight",
            WebhookEventType = OrgWebhookEventTypes.BuildExpiring,
        })).ToList();

        return await SendAsync(due, BuildThresholds, now, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> NotifyComplianceDeadlinesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var catalog = _rulesCatalog.Current;

        if (catalog == null)
            return 0;

        var today = DateOnly.FromDateTime(now);
        var horizon = today.AddDays(ComplianceThresholds.Max());
        var language = CultureInfo.CurrentUICulture.Name;
        var organizations = await _organizationRepository.GetAllAsync(o => !o.IsDeleted, projection: o => new Organization { Id = o.Id }, cancellationToken: cancellationToken);
        var due = new List<Reminder>();

        foreach (var organization in organizations)
        {
            var records = await _contentService.GetStoreAppsAsync(organization.Id, null, cancellationToken);

            if (records.Count == 0)
                continue;

            var steps = await _readinessService.GetStepsAsync(organization.Id, null, cancellationToken);

            var items = records.SelectMany(r => ComplianceTimeline.Evaluate(r, _contentService.Subject(r, null), catalog, steps.GetValueOrDefault((r.AppId, r.Store)) ?? [], today, language))
                               .Where(i => i.DueDate > today && i.DueDate <= horizon && i.Status != ComplianceStatus.Done);

            due.AddRange(items.Select(i => ComplianceReminder(organization.Id, i, now)));
        }

        return await SendAsync(due, ComplianceThresholds, now, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> NotifyMsSecretExpiryAsync(DateTime now, CancellationToken cancellationToken)
    {
        var connections = await _msConnectionExpiryReader.GetExpiringAsync(now.AddDays(MsSecretThresholds.Max()), null, cancellationToken);

        var due = connections.Where(c => c.CredentialExpiryDate > now).Select(c => new Reminder(c.OrganizationId, $"ms-secret:{c.ConnectionId}:{c.CredentialExpiryDate:yyyyMMdd}", c.CredentialExpiryDate, new OrgNotification
        {
            OrganizationId = c.OrganizationId,
            Kind = NotificationType.ConnectionAlert,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationMsSecretExpiring,
            TextArgs = [c.Name, c.CredentialExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DaysLeft(c.CredentialExpiryDate, now)],
            Data = new { connectionId = c.ConnectionId, expiryDate = c.CredentialExpiryDate, daysLeft = DaysLeft(c.CredentialExpiryDate, now) },
            RelatedEntity = NotificationEntity.StoreConnection,
            RelatedEntityId = c.ConnectionId.ToString(CultureInfo.InvariantCulture),
            ActionLink = $"/connections/{c.ConnectionId}",
            WebhookEventType = OrgWebhookEventTypes.ConnectionSecretExpiring,
        })).ToList();

        return await SendAsync(due, MsSecretThresholds, now, cancellationToken);
    }

    private static Reminder ComplianceReminder(int organizationId, ComplianceItemDto item, DateTime now)
    {
        var dueDate = item.DueDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return new Reminder(organizationId, $"rule:{item.RuleId}:app:{item.AppId}:{dueDate:yyyyMMdd}", dueDate, new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.Deadline,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationComplianceDeadline,
            TextArgs = [item.AppName, item.Title, dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DaysLeft(dueDate, now)],
            Data = new { appId = item.AppId, ruleId = item.RuleId, store = item.Store.ToString(), dueDate, item.SourceUrl, item.MatchReason, status = item.Status.ToString(), daysLeft = DaysLeft(dueDate, now) },
            AppId = item.AppId,
            RelatedEntity = NotificationEntity.Rule,
            RelatedEntityId = item.RuleId,
            ActionLink = $"/apps/{item.AppId}/compliance",
            WebhookEventType = OrgWebhookEventTypes.ComplianceDeadline,
        });
    }

    private async Task<int> SendAsync(List<Reminder> due, int[] thresholds, DateTime now, CancellationToken cancellationToken)
    {
        if (due.Count == 0)
            return 0;

        var subjects = due.Select(d => d.Subject).Distinct().ToList();
        var sent = (await _reminderRepository.GetAllAsync(r => subjects.Contains(r.Subject),
                                                          projection: r => new DeadlineReminder { Subject = r.Subject, ThresholdDays = r.ThresholdDays },
                                                          cancellationToken: cancellationToken)).Select(r => (r.Subject, r.ThresholdDays)).ToHashSet();

        var send = new List<(Reminder Reminder, int Threshold)>();

        foreach (var reminder in due.DistinctBy(d => d.Subject))
        {
            var daysLeft = DaysLeft(reminder.DueDate, now);
            var threshold = thresholds.Where(t => daysLeft <= t).DefaultIfEmpty(0).Min();

            if (threshold == 0 || sent.Contains((reminder.Subject, threshold)))
                continue;

            send.Add((reminder, threshold));
        }

        if (send.Count == 0)
            return 0;

        try
        {
            await _reminderRepository.AddRangeAsync([.. send.Select(s => new DeadlineReminder { OrganizationId = s.Reminder.OrganizationId, Subject = s.Reminder.Subject, ThresholdDays = s.Threshold, SentDate = now })], cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another run sent them.
            return 0;
        }

        foreach (var (reminder, _) in send)
            await _notificationDispatcher.DispatchAsync(reminder.Notification, cancellationToken);

        return send.Count;
    }

    private static int DaysLeft(DateTime dueDate, DateTime now) => Math.Max(0, (int)Math.Ceiling((dueDate - now).TotalDays));

    private sealed record Reminder(int OrganizationId, string Subject, DateTime DueDate, OrgNotification Notification);
}
