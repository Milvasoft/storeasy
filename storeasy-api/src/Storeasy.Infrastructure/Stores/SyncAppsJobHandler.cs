using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores;

/// <summary>
/// The <see cref="StoreJobKind.SyncApps"/> job: reads the apps and builds of one store connection and saves them.
/// <list type="bullet">
/// <item>App Store Connect: every app the key sees (<c>/v1/apps</c>); a new one is linked to the storeasy app whose other
/// store record has the same bundle id, otherwise it gets its own storeasy app.</item>
/// <item>Google Play: no list API, so the records added by package name are read one by one (the edit probe proves the
/// service account's access). A record Play refuses that was never confirmed is removed with its empty app.</item>
/// <item>Microsoft Store: not supported yet (permanent failure).</item>
/// </list>
/// Builds are upserted by the store's build id. All writes are set based or bulk; the loops only call the store.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="appRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="storeGateway"></param>
/// <param name="activityPublisher"></param>
/// <param name="timeProvider"></param>
public sealed class SyncAppsJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                       IStoreasyRepositoryBase<App> appRepository,
                                       IStoreasyRepositoryBase<Build> buildRepository,
                                       IStoreGateway storeGateway,
                                       IActivityPublisher activityPublisher,
                                       TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreGateway _storeGateway = storeGateway;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncApps;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncAppsPayload>() ?? throw new StoreJobException("The job has no connection.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        if (connection.Store is not (StoreKind.Apple or StoreKind.Google))
            throw new StoreJobException($"{connection.Store} is not supported yet.", transient: false);

        var known = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId && s.Store == connection.Store,
                                                          projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, ExternalId = s.ExternalId, BundleId = s.BundleId, Name = s.Name, StoreConnectionId = s.StoreConnectionId, SyncedDate = s.SyncedDate },
                                                          cancellationToken: cancellationToken);

        // 1. Read the apps from the store.
        var listStep = await context.BeginStepAsync("apps.list", StepDirection.Outbound, $"{connection.Store} connection '{connection.Name}'", cancellationToken: cancellationToken);

        List<StoreAppRecord> records;
        List<StoreApp> refused = [];

        if (connection.Store == StoreKind.Apple)
            records = [.. await _storeGateway.ListAppsAsync(connection.Store, connection.SecretJson, cancellationToken)];
        else
            (records, refused) = await ReadGoogleAppsAsync(connection, known.Where(s => s.StoreConnectionId == connection.Id).ToList(), cancellationToken);

        await context.EndStepAsync(listStep, StoreJobStepStatus.Succeeded, refused.Count == 0 ? $"{records.Count} apps." : $"{records.Count} apps; refused: {string.Join(", ", refused.Select(r => r.ExternalId))}.", cancellationToken);

        // 2. Save them: update the known records, link or create the new ones, drop never confirmed records the store refused.
        var saveStep = await context.BeginStepAsync("apps.save", StepDirection.Internal, cancellationToken: cancellationToken);

        var synced = await SaveAppsAsync(context.OrganizationId, connection, records, known, refused, cancellationToken);

        await context.EndStepAsync(saveStep, StoreJobStepStatus.Succeeded, $"{synced.Count} records saved.", cancellationToken);

        // 3. Read the builds of every record.
        var buildStep = await context.BeginStepAsync("builds.list", StepDirection.Outbound, total: synced.Count, cancellationToken: cancellationToken);
        var builds = new List<(StoreApp Record, StoreBuildRecord Build)>();

        for (var i = 0; i < synced.Count; i++)
        {
            builds.AddRange((await _storeGateway.ListBuildsAsync(connection.Store, connection.SecretJson, synced[i].ExternalId, cancellationToken)).Select(b => (synced[i], b)));

            await context.ReportProgressAsync(buildStep, i + 1, cancellationToken);
        }

        await context.EndStepAsync(buildStep, StoreJobStepStatus.Succeeded, $"{builds.Count} builds.", cancellationToken);

        // 4. Save the builds.
        var buildSaveStep = await context.BeginStepAsync("builds.save", StepDirection.Internal, cancellationToken: cancellationToken);

        var added = await SaveBuildsAsync(context.OrganizationId, connection.Store, synced, builds, cancellationToken);

        await context.EndStepAsync(buildSaveStep, StoreJobStepStatus.Succeeded, $"{added} new builds.", cancellationToken);

        foreach (var appId in synced.Select(s => s.AppId).Distinct())
        {
            await _activityPublisher.PublishAsync(new ActivityEventDto
            {
                Type = ActivityEventTypes.AppSynced,
                OrganizationId = context.OrganizationId,
                JobId = context.JobId,
                Store = connection.Store,
                AppId = appId,
                ConnectionId = connection.Id,
                Date = _timeProvider.GetUtcNow().UtcDateTime,
            }, cancellationToken);
        }

        return new StoreJobOutcome($"{synced.Count} apps and {builds.Count} builds synced from {connection.Store} ({added} new builds).",
                                   new { Apps = synced.Count, Builds = builds.Count, NewBuilds = added, Refused = refused.Select(r => r.ExternalId).ToList() });
    }

    /// <summary>
    /// Reads the Play records one by one. A permanent refusal (no access, unknown package) is collected; a transient
    /// failure fails the attempt so the job is retried.
    /// </summary>
    private async Task<(List<StoreAppRecord> Records, List<StoreApp> Refused)> ReadGoogleAppsAsync(OpenedStoreConnection connection, List<StoreApp> records, CancellationToken cancellationToken)
    {
        var read = new List<StoreAppRecord>();
        var refused = new List<StoreApp>();

        foreach (var record in records)
        {
            try
            {
                read.Add(await _storeGateway.GetAppAsync(connection.Store, connection.SecretJson, record.ExternalId, cancellationToken));
            }
            catch (StoreCallException ex) when (!ex.Transient)
            {
                refused.Add(record);
            }
        }

        return (read, refused);
    }

    private async Task<List<StoreApp>> SaveAppsAsync(int organizationId,
                                                     OpenedStoreConnection connection,
                                                     List<StoreAppRecord> records,
                                                     List<StoreApp> known,
                                                     List<StoreApp> refused,
                                                     CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var knownByExternalId = known.ToDictionary(s => s.ExternalId);

        var updated = records.Where(r => knownByExternalId.ContainsKey(r.ExternalId)).Select(r =>
        {
            var record = knownByExternalId[r.ExternalId];

            record.Name = r.Name ?? record.Name;
            record.BundleId = r.BundleId ?? record.BundleId;
            record.StoreConnectionId = connection.Id;
            record.SyncedDate = now;

            return record;
        }).ToList();

        if (updated.Count > 0)
            await _storeAppRepository.UpdateAsync(updated, cancellationToken, s => s.Name, s => s.BundleId, s => s.StoreConnectionId, s => s.SyncedDate);

        var created = await CreateRecordsAsync(organizationId, connection, records.Where(r => !knownByExternalId.ContainsKey(r.ExternalId)).ToList(), now, cancellationToken);

        var neverConfirmed = refused.Where(r => r.SyncedDate == null).ToList();

        if (neverConfirmed.Count > 0)
        {
            var ids = neverConfirmed.Select(r => r.Id).ToList();
            var appIds = neverConfirmed.Select(r => r.AppId).ToList();

            await _storeAppRepository.ExecuteDeleteAsync(s => ids.Contains(s.Id), cancellationToken: cancellationToken);
            await _appRepository.ExecuteDeleteAsync(a => appIds.Contains(a.Id) && !a.StoreApps.Any(), cancellationToken: cancellationToken);
        }

        return [.. updated, .. created];
    }

    /// <summary>
    /// Adds the new records: each joins the storeasy app whose record in another store has the same platform identifier
    /// (and no record in this store yet), otherwise a new storeasy app named after it.
    /// </summary>
    private async Task<List<StoreApp>> CreateRecordsAsync(int organizationId, OpenedStoreConnection connection, List<StoreAppRecord> records, DateTime now, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
            return [];

        var candidates = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == organizationId && s.Store != connection.Store && s.BundleId != null && !s.App.IsDeleted,
                                                               projection: s => new StoreApp { AppId = s.AppId, BundleId = s.BundleId },
                                                               cancellationToken: cancellationToken);

        var taken = (await _storeAppRepository.GetAllAsync(s => s.OrganizationId == organizationId && s.Store == connection.Store,
                                                           projection: s => new StoreApp { AppId = s.AppId },
                                                           cancellationToken: cancellationToken)).Select(s => s.AppId).ToHashSet();

        var linked = new List<StoreApp>();
        var newApps = new List<App>();

        foreach (var record in records)
        {
            var storeApp = new StoreApp
            {
                OrganizationId = organizationId,
                StoreConnectionId = connection.Id,
                Store = connection.Store,
                ExternalId = record.ExternalId,
                BundleId = record.BundleId,
                Name = record.Name,
                SyncedDate = now,
            };

            var key = AppRules.LinkKey(record.BundleId);
            var match = key == null ? null : candidates.FirstOrDefault(c => AppRules.LinkKey(c.BundleId) == key && !taken.Contains(c.AppId));

            if (match != null)
            {
                storeApp.AppId = match.AppId;
                taken.Add(match.AppId);
                linked.Add(storeApp);
            }
            else
            {
                newApps.Add(new App { OrganizationId = organizationId, Name = record.Name ?? record.ExternalId, StoreApps = [storeApp] });
            }
        }

        if (linked.Count > 0)
            await _storeAppRepository.AddRangeAsync(linked, cancellationToken);

        if (newApps.Count > 0)
            await _appRepository.AddRangeAsync(newApps, cancellationToken);

        return [.. linked, .. newApps.SelectMany(a => a.StoreApps)];
    }

    private async Task<int> SaveBuildsAsync(int organizationId, StoreKind store, List<StoreApp> records, List<(StoreApp Record, StoreBuildRecord Build)> builds, CancellationToken cancellationToken)
    {
        if (builds.Count == 0)
            return 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var recordIds = records.Select(r => r.Id).ToList();

        var existing = (await _buildRepository.GetAllAsync(b => recordIds.Contains(b.StoreAppId),
                                                           projection: b => new Build { Id = b.Id, StoreAppId = b.StoreAppId, ExternalId = b.ExternalId },
                                                           cancellationToken: cancellationToken)).ToDictionary(b => (b.StoreAppId, b.ExternalId));

        var updated = new List<Build>();
        var added = new List<Build>();

        foreach (var (record, build) in builds.DistinctBy(b => (b.Record.Id, b.Build.ExternalId)))
        {
            var row = existing.GetValueOrDefault((record.Id, build.ExternalId)) ?? new Build { OrganizationId = organizationId, StoreAppId = record.Id, Store = store, ExternalId = build.ExternalId };

            row.Version = Trim(build.Version);
            row.BuildNumber = Trim(build.BuildNumber);
            row.ProcessingState = build.ProcessingState;
            row.Tracks = Trim(build.Tracks);
            row.MinOsVersion = Trim(build.MinOsVersion);
            row.UploadedDate = build.UploadedDate;
            row.ExpiryDate = build.ExpiryDate;
            row.SyncedDate = now;

            (row.Id == 0 ? added : updated).Add(row);
        }

        if (updated.Count > 0)
            await _buildRepository.UpdateAsync(updated, cancellationToken, b => b.Version, b => b.BuildNumber, b => b.ProcessingState, b => b.Tracks, b => b.MinOsVersion, b => b.UploadedDate, b => b.ExpiryDate, b => b.SyncedDate);

        if (added.Count > 0)
            await _buildRepository.AddRangeAsync(added, cancellationToken);

        return added.Count;
    }

    private static string Trim(string value) => value?.Length > Build.TextMaxLength ? value[..Build.TextMaxLength] : value;
}
