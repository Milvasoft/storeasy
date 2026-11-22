using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Enums;
using System.Globalization;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// <see cref="StoreJobKind.ChromeSync"/>: reads the Chrome Web Store items of a connection with <c>fetchStatus</c> (the
/// published and the submitted revision, deploy percentage, last upload, policy flags), their rating summary and seven day
/// users; the versions become builds. A state change (in review, staged, published, rejected, cancelled, warned, taken down)
/// is audited and notified like a store version state change. The schedule runs it every 15 minutes for items with a
/// submission in review or an upload in progress, reading only those.
/// </summary>
/// <param name="client"></param>
/// <param name="storeAppRepository"></param>
/// <param name="itemRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="timeProvider"></param>
public sealed class ChromeSyncJobHandler(IChromeWebStoreClient client,
                                         IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                         IStoreasyRepositoryBase<ChromeItem> itemRepository,
                                         IStoreasyRepositoryBase<Build> buildRepository,
                                         IAuditTrail auditTrail,
                                         INotificationDispatcher notificationDispatcher,
                                         TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IChromeWebStoreClient _client = client;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = itemRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.ChromeSync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<ChromeSyncPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store != StoreKind.Chrome)
            throw new StoreJobException($"{connection.Store} is not a Chrome Web Store connection.", transient: false);

        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId
                                                                 && s.Store == StoreKind.Chrome
                                                                 && s.StoreConnectionId == connection.Id
                                                                 && !s.App.IsDeleted
                                                                 && (payload.StoreAppId == null || s.Id == payload.StoreAppId),
                                                            projection: s => new StoreApp { Id = s.Id, OrganizationId = s.OrganizationId, AppId = s.AppId, ExternalId = s.ExternalId, Name = s.Name, App = new App { Name = s.App.Name } },
                                                            cancellationToken: cancellationToken);

        var recordIds = records.Select(r => r.Id).ToList();
        var items = (await _itemRepository.GetAllAsync(i => recordIds.Contains(i.StoreAppId), cancellationToken: cancellationToken)).ToDictionary(i => i.StoreAppId);

        // A record linked without the add command (apps_link_store_record) gets its row here.
        var missing = records.Where(r => !items.ContainsKey(r.Id))
                             .Select(r => new ChromeItem { OrganizationId = context.OrganizationId, StoreAppId = r.Id, ItemId = r.ExternalId })
                             .ToList();

        if (missing.Count > 0)
        {
            await _itemRepository.AddRangeAsync(missing, cancellationToken);

            foreach (var item in missing)
                items[item.StoreAppId] = item;
        }

        if (payload.PendingOnly)
            records = [.. records.Where(r => items[r.Id].IsPending)];

        var step = await context.BeginStepAsync("chrome.items.read", StepDirection.Inbound, connection.Name, records.Count, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var read = 0;
        var changed = 0;
        var failed = new List<string>();
        var updated = new List<ChromeItem>();
        var builds = new List<(StoreApp Record, StoreBuildRecord Build)>();
        var reports = new List<(StoreApp Record, ChromeItem Item, ChromeStateTransition Transition)>();

        foreach (var record in records)
        {
            var item = items[record.Id];

            try
            {
                var status = await _client.FetchStatusAsync(connection.SecretJson, record.ExternalId, cancellationToken);
                var transitions = ChromeItemStates.Merge(item, status, now);

                if (!payload.PendingOnly)
                    await ReadExtrasAsync(connection.SecretJson, record.ExternalId, item, now, cancellationToken);

                builds.AddRange(ChromeChannel.BuildsOf(status).Select(b => (record, b)));
                reports.AddRange(transitions.Select(t => (record, item, t)));
                changed += transitions.Count;
                read++;
            }
            catch (StoreCallException ex) when (!ex.Transient)
            {
                item.SyncError = SecretRedactor.RedactAndTrim(ex.Message, ChromeItem.SyncErrorMaxLength);
                failed.Add(record.ExternalId);
            }

            updated.Add(item);
            await context.ReportProgressAsync(step, read + failed.Count, cancellationToken);
        }

        if (updated.Count > 0)
            await _itemRepository.UpdateAsync(updated, cancellationToken);

        await SaveBuildsAsync(recordIds, builds, now, cancellationToken);

        foreach (var (record, item, transition) in reports)
            await ReportAsync(context.OrganizationId, record, item, transition, cancellationToken);

        await context.EndStepAsync(step,
                                   failed.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   failed.Count == 0 ? $"{read} items, {changed} changes." : $"{read} items; not read: {string.Join(", ", failed)}.",
                                   cancellationToken);

        return new StoreJobOutcome($"Chrome Web Store: {read} items read, {changed} state changes.", new { Items = read, Failed = failed, Changed = changed });
    }

    /// <summary>
    /// The rating summary and the latest seven day users; either may be missing (a new item has no metrics yet), so a refusal
    /// leaves the old values.
    /// </summary>
    private async Task ReadExtrasAsync(string secretJson, string itemId, ChromeItem item, DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _client.GetRatingSummaryAsync(secretJson, itemId, cancellationToken);

            item.Rating = summary.Rating;
            item.RatingCount = summary.Count;
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
        }

        try
        {
            var today = DateOnly.FromDateTime(now);
            var rows = await _client.QueryMetricsAsync(secretJson, itemId, ["users-7d"], null, today.AddDays(-7), today, cancellationToken);
            var latest = rows.Where(r => r.Values.ContainsKey("users-7d")).OrderByDescending(r => r.Date).FirstOrDefault();

            if (latest != null)
                item.WeeklyUsers = latest.Values["users-7d"];
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
        }
    }

    private async Task SaveBuildsAsync(List<int> recordIds, List<(StoreApp Record, StoreBuildRecord Build)> builds, DateTime now, CancellationToken cancellationToken)
    {
        if (builds.Count == 0)
            return;

        var known = (await _buildRepository.GetAllAsync(b => recordIds.Contains(b.StoreAppId), projection: b => new Build { StoreAppId = b.StoreAppId, ExternalId = b.ExternalId }, cancellationToken: cancellationToken))
                    .Select(b => (b.StoreAppId, b.ExternalId))
                    .ToHashSet();
        var added = builds.Where(b => !known.Contains((b.Record.Id, b.Build.ExternalId)) && b.Build.ExternalId.Length <= Build.TextMaxLength).Select(pair => (Record: pair.Record, b: pair.Build)).Select(x => new Build
        {
            OrganizationId = x.Record.OrganizationId,
            StoreAppId = x.Record.Id,
            Store = StoreKind.Chrome,
            ExternalId = x.b.ExternalId,
            Version = x.b.Version,
            BuildNumber = x.b.BuildNumber,
            ProcessingState = x.b.ProcessingState,
            SyncedDate = now,
        }).ToList();

        if (added.Count > 0)
            await _buildRepository.AddRangeAsync(added, cancellationToken);
    }

    private async Task ReportAsync(int organizationId, StoreApp record, ChromeItem item, ChromeStateTransition transition, CancellationToken cancellationToken)
    {
        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteAsync(organizationId, transition.Action, record.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{record.ExternalId} {transition.Version} ({transition.Revision}): {transition.Previous} -> {transition.Current}.", StoreKind.Chrome,
                                     new { item.ItemId, transition.Revision, transition.Version, transition.Previous, transition.Current }, cancellationToken);

        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.VersionState,
            Roles = NotificationAudiences.Editors,
            TextKey = MessageKey.NotificationChromeState,
            TextArgs = [record.App?.Name ?? record.Name, transition.Version ?? "-", transition.Previous, transition.Current],
            Data = new { appId = record.AppId, storeAppId = record.Id, store = StoreKind.Chrome.ToString(), revision = transition.Revision, versionString = transition.Version, oldValue = transition.Previous, newValue = transition.Current },
            AppId = record.AppId,
            RelatedEntity = NotificationEntity.AppVersionState,
            RelatedEntityId = $"{record.ExternalId}:{transition.Revision}",
            ActionLink = $"/apps/{record.AppId}/chrome",
            WebhookEventType = OrgWebhookEventTypes.VersionStateChanged,
            ActivityType = ActivityEventTypes.VersionStateChanged,
        }, cancellationToken);
    }
}
