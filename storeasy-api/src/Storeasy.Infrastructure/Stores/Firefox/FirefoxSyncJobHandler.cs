using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Firefox;
using System.Globalization;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// <see cref="StoreJobKind.FirefoxSync"/>: reads the Firefox add-ons of a connection from addons.mozilla.org. A full sync of a
/// connection first lists the account's add-ons and adds a storeasy app for each new one. Every add-on is then read (status,
/// listed and unlisted versions with their file status, rating, users); the versions become builds, the record is fixed to the
/// numeric id and the guid, and a state change (awaiting review, approved, disabled) is audited and notified like a store
/// version state change. The schedule runs it every 15 minutes for add-ons with a version awaiting review, reading only those.
/// </summary>
/// <param name="client"></param>
/// <param name="storeAppRepository"></param>
/// <param name="addonRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class FirefoxSyncJobHandler(IAmoClient client,
                                          IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                          IStoreasyRepositoryBase<FirefoxAddon> addonRepository,
                                          IStoreasyRepositoryBase<Build> buildRepository,
                                          IAuditTrail auditTrail,
                                          INotificationDispatcher notificationDispatcher,
                                          FirefoxOptions options,
                                          TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IAmoClient _client = client;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<FirefoxAddon> _addonRepository = addonRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly FirefoxOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.FirefoxSync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<FirefoxSyncPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store != StoreKind.Firefox)
            throw new StoreJobException($"{connection.Store} is not a Firefox Add-ons connection.", transient: false);

        var added = 0;

        if (payload.StoreAppId == null && !payload.PendingOnly)
            added = await AddNewAddonsAsync(context, connection, cancellationToken);

        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId
                                                                 && s.Store == StoreKind.Firefox
                                                                 && s.StoreConnectionId == connection.Id
                                                                 && !s.App.IsDeleted
                                                                 && (payload.StoreAppId == null || s.Id == payload.StoreAppId),
                                                            projection: s => new StoreApp { Id = s.Id, OrganizationId = s.OrganizationId, AppId = s.AppId, ExternalId = s.ExternalId, BundleId = s.BundleId, Name = s.Name, App = new App { Name = s.App.Name } },
                                                            cancellationToken: cancellationToken);

        var recordIds = records.Select(r => r.Id).ToList();
        var rows = (await _addonRepository.GetAllAsync(a => recordIds.Contains(a.StoreAppId), cancellationToken: cancellationToken)).ToDictionary(a => a.StoreAppId);

        // A record linked without the add command (apps_link_store_record) gets its row here.
        var missing = records.Where(r => !rows.ContainsKey(r.Id))
                             .Select(r => new FirefoxAddon { OrganizationId = context.OrganizationId, StoreAppId = r.Id, Guid = r.BundleId })
                             .ToList();

        if (missing.Count > 0)
        {
            await _addonRepository.AddRangeAsync(missing, cancellationToken);

            foreach (var row in missing)
                rows[row.StoreAppId] = row;
        }

        if (payload.PendingOnly)
            records = [.. records.Where(r => rows[r.Id].IsPending)];

        var step = await context.BeginStepAsync("firefox.addons.read", StepDirection.Inbound, connection.Name, records.Count, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var read = 0;
        var changed = 0;
        var failed = new List<string>();
        var updated = new List<FirefoxAddon>();
        var renamed = new List<StoreApp>();
        var builds = new List<(StoreApp Record, StoreBuildRecord Build)>();
        var reports = new List<(StoreApp Record, FirefoxAddon Row, FirefoxStateTransition Transition)>();

        foreach (var record in records)
        {
            var row = rows[record.Id];

            try
            {
                var state = await FirefoxContentGateway.ReadStateAsync(_client, connection.SecretJson, record.ExternalId, _options.VersionPages, cancellationToken);
                var transitions = FirefoxAddonStates.Merge(row, state, now);
                var externalId = state.Addon.Id.ToString(CultureInfo.InvariantCulture);

                // The record keeps the numeric id and the guid, whatever it was added by.
                if (state.Addon.Id > 0 && (record.ExternalId != externalId || record.BundleId != state.Addon.Guid))
                {
                    record.ExternalId = externalId;
                    record.BundleId = state.Addon.Guid;
                    renamed.Add(record);
                }

                builds.AddRange(FirefoxChannel.BuildsOf(state.Versions).Select(b => (record, b)));
                reports.AddRange(transitions.Select(t => (record, row, t)));
                changed += transitions.Count;
                read++;
            }
            catch (StoreCallException ex) when (!ex.Transient)
            {
                row.SyncError = SecretRedactor.RedactAndTrim(ex.Message, FirefoxAddon.SyncErrorMaxLength);
                failed.Add(record.ExternalId);
            }

            updated.Add(row);
            await context.ReportProgressAsync(step, read + failed.Count, cancellationToken);
        }

        if (updated.Count > 0)
            await _addonRepository.UpdateAsync(updated, cancellationToken);

        if (renamed.Count > 0)
            await _storeAppRepository.UpdateAsync(renamed, cancellationToken, s => s.ExternalId, s => s.BundleId);

        await SaveBuildsAsync(recordIds, builds, now, cancellationToken);

        foreach (var (record, row, transition) in reports)
            await ReportAsync(context.OrganizationId, record, row, transition, cancellationToken);

        await context.EndStepAsync(step,
                                   failed.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   failed.Count == 0 ? $"{read} add-ons, {changed} changes." : $"{read} add-ons; not read: {string.Join(", ", failed)}.",
                                   cancellationToken);

        return new StoreJobOutcome($"Firefox Add-ons: {read} add-ons read, {added} added, {changed} state changes.", new { Addons = read, Added = added, Failed = failed, Changed = changed });
    }

    /// <summary>
    /// Adds a storeasy app, a store record and an add-on row for every add-on of the account storeasy does not know yet.
    /// </summary>
    private async Task<int> AddNewAddonsAsync(IStoreJobContext context, OpenedStoreConnection connection, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("firefox.addons.list", StepDirection.Inbound, connection.Name, cancellationToken: cancellationToken);
        var addons = await _client.ListAddonsAsync(connection.SecretJson, cancellationToken);

        var known = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId && s.Store == StoreKind.Firefox,
                                                         projection: s => new StoreApp { ExternalId = s.ExternalId, BundleId = s.BundleId },
                                                         cancellationToken: cancellationToken);

        var knownIds = known.Select(s => s.ExternalId).Concat(known.Where(s => s.BundleId != null).Select(s => s.BundleId)).ToHashSet(StringComparer.Ordinal);
        var rows = new List<FirefoxAddon>();

        foreach (var addon in addons.Where(a => a.Id > 0 && a.Status != FirefoxAddonStatus.Deleted))
        {
            var record = FirefoxChannel.RecordOf(addon);

            if (knownIds.Contains(record.ExternalId) || (record.BundleId != null && knownIds.Contains(record.BundleId)))
                continue;

            var name = (record.Name ?? record.ExternalId).Length > App.NameMaxLength ? (record.Name ?? record.ExternalId)[..App.NameMaxLength] : record.Name ?? record.ExternalId;
            var storeApp = new StoreApp
            {
                OrganizationId = context.OrganizationId,
                StoreConnectionId = connection.Id,
                Store = StoreKind.Firefox,
                ExternalId = record.ExternalId,
                BundleId = record.BundleId,
                Name = name,
                App = new App { OrganizationId = context.OrganizationId, Name = name, IconUrl = addon.IconUrl?.Length > App.IconUrlMaxLength ? null : addon.IconUrl },
            };

            rows.Add(new FirefoxAddon { OrganizationId = context.OrganizationId, AddonId = addon.Id, Guid = addon.Guid, Slug = addon.Slug, StoreApp = storeApp });
            knownIds.Add(record.ExternalId);
        }

        // One insert for the apps, the records and the add-on rows.
        if (rows.Count > 0)
            await _addonRepository.AddRangeAsync(rows, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{addons.Count} add-ons, {rows.Count} new.", cancellationToken);

        foreach (var storeApp in rows.Select(r => r.StoreApp))
        {
            await _auditTrail.WriteAsync(context.OrganizationId, AuditAction.FirefoxAddonAdded, storeApp.Id.ToString(CultureInfo.InvariantCulture), $"Firefox add-on {storeApp.BundleId ?? storeApp.ExternalId} added from the account.", StoreKind.Firefox, cancellationToken: cancellationToken);
        }

        return rows.Count;
    }

    private async Task SaveBuildsAsync(List<int> recordIds, List<(StoreApp Record, StoreBuildRecord Build)> builds, DateTime now, CancellationToken cancellationToken)
    {
        if (builds.Count == 0)
            return;

        var existing = (await _buildRepository.GetAllAsync(b => recordIds.Contains(b.StoreAppId), projection: b => new Build { Id = b.Id, StoreAppId = b.StoreAppId, ExternalId = b.ExternalId, ProcessingState = b.ProcessingState }, cancellationToken: cancellationToken))
                       .ToDictionary(b => (b.StoreAppId, b.ExternalId));

        var added = new List<Build>();
        var changed = new List<Build>();

        foreach (var (record, build) in builds.DistinctBy(b => (b.Record.Id, b.Build.ExternalId)))
        {
            if (existing.TryGetValue((record.Id, build.ExternalId), out var row))
            {
                if (row.ProcessingState != build.ProcessingState)
                {
                    row.ProcessingState = build.ProcessingState;
                    row.SyncedDate = now;
                    changed.Add(row);
                }

                continue;
            }

            if (build.ExternalId.Length > Build.TextMaxLength || build.Version?.Length > Build.TextMaxLength)
                continue;

            added.Add(new Build
            {
                OrganizationId = record.OrganizationId,
                StoreAppId = record.Id,
                Store = StoreKind.Firefox,
                ExternalId = build.ExternalId,
                Version = build.Version,
                BuildNumber = build.BuildNumber,
                ProcessingState = build.ProcessingState,
                UploadedDate = build.UploadedDate,
                SyncedDate = now,
            });
        }

        if (added.Count > 0)
            await _buildRepository.AddRangeAsync(added, cancellationToken);

        if (changed.Count > 0)
            await _buildRepository.UpdateAsync(changed, cancellationToken, b => b.ProcessingState, b => b.SyncedDate);
    }

    private async Task ReportAsync(int organizationId, StoreApp record, FirefoxAddon row, FirefoxStateTransition transition, CancellationToken cancellationToken)
    {
        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteAsync(organizationId, transition.Action, record.Id.ToString(CultureInfo.InvariantCulture),
                                     $"{row.Guid ?? record.ExternalId} {transition.Version} ({transition.Subject}): {transition.Previous} -> {transition.Current}.", StoreKind.Firefox,
                                     new { row.AddonId, row.Guid, transition.Subject, transition.Version, Channel = transition.Channel.ToString(), transition.Previous, transition.Current }, cancellationToken);

        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = organizationId,
            Kind = NotificationType.VersionState,
            Roles = NotificationAudiences.Editors,
            TextKey = MessageKey.NotificationFirefoxState,
            TextArgs = [record.App?.Name ?? record.Name, transition.Version ?? "-", transition.Previous, transition.Current],
            Data = new { appId = record.AppId, storeAppId = record.Id, store = StoreKind.Firefox.ToString(), subject = transition.Subject, channel = transition.Channel.ToString(), versionString = transition.Version, oldValue = transition.Previous, newValue = transition.Current },
            AppId = record.AppId,
            RelatedEntity = NotificationEntity.AppVersionState,
            RelatedEntityId = $"{record.ExternalId}:{transition.Subject}",
            ActionLink = $"/apps/{record.AppId}/firefox",
            WebhookEventType = OrgWebhookEventTypes.VersionStateChanged,
            ActivityType = ActivityEventTypes.VersionStateChanged,
        }, cancellationToken);
    }
}
