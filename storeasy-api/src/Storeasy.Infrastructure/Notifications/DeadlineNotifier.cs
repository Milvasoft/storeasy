using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Notifications;
using System.Globalization;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IDeadlineNotifier"/>. Each run sends, per subject, only the nearest threshold not sent yet (a build
/// first seen 5 days before expiry gets the 7 day reminder, not the 14 day one). The sent reminders are written before
/// the notifications go out, so two runs never send one twice (unique subject and threshold).
/// </summary>
/// <param name="buildRepository"></param>
/// <param name="appRepository"></param>
/// <param name="reminderRepository"></param>
/// <param name="catalogSource"></param>
/// <param name="notificationDispatcher"></param>
public sealed class DeadlineNotifier(IStoreasyRepositoryBase<Build> buildRepository,
                                     IStoreasyRepositoryBase<App> appRepository,
                                     IStoreasyRepositoryBase<DeadlineReminder> reminderRepository,
                                     IRulesCatalogSource catalogSource,
                                     INotificationDispatcher notificationDispatcher) : IDeadlineNotifier
{
    /// <summary>
    /// TestFlight expiry thresholds in days.
    /// </summary>
    public static readonly int[] BuildThresholds = [14, 7, 1];

    /// <summary>
    /// Compliance deadline thresholds in days.
    /// </summary>
    public static readonly int[] ComplianceThresholds = [30, 7, 1];

    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<DeadlineReminder> _reminderRepository = reminderRepository;
    private readonly IRulesCatalogSource _catalogSource = catalogSource;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;

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
        var apps = await _appRepository.GetAllAsync(a => !a.IsDeleted && a.StoreApps.Any(),
                                                    projection: a => new { a.Id, a.OrganizationId, a.Name, Stores = a.StoreApps.Select(s => s.Store).ToList() },
                                                    cancellationToken: cancellationToken);

        if (apps.Count == 0)
            return 0;

        var contexts = apps.Select(a => new CatalogAppContext(a.Id, a.OrganizationId, a.Name, a.Stores)).ToList();
        var deadlines = await _catalogSource.GetDeadlinesAsync(contexts, now, now.AddDays(ComplianceThresholds.Max()), cancellationToken);
        var byId = apps.ToDictionary(a => a.Id);

        var due = deadlines.Where(d => byId.ContainsKey(d.AppId) && d.DueDate > now).Select(d =>
        {
            var app = byId[d.AppId];

            return new Reminder(app.OrganizationId, $"rule:{d.RuleId}:app:{d.AppId}:{d.DueDate:yyyyMMdd}", d.DueDate, new OrgNotification
            {
                OrganizationId = app.OrganizationId,
                Kind = NotificationType.Deadline,
                Roles = NotificationAudiences.OwnersAndAdmins,
                TextKey = MessageKey.NotificationComplianceDeadline,
                TextArgs = [app.Name, d.Title, d.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DaysLeft(d.DueDate, now)],
                Data = new { appId = d.AppId, ruleId = d.RuleId, store = d.Store.ToString(), dueDate = d.DueDate, d.SourceUrl, d.MatchReason, daysLeft = DaysLeft(d.DueDate, now) },
                AppId = d.AppId,
                RelatedEntity = NotificationEntity.Rule,
                RelatedEntityId = d.RuleId,
                ActionLink = $"/apps/{d.AppId}/compliance",
                WebhookEventType = OrgWebhookEventTypes.ComplianceDeadline,
            });
        }).ToList();

        return await SendAsync(due, ComplianceThresholds, now, cancellationToken);
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
