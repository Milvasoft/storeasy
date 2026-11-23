using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="IRegistrySyncer"/>: reads every package from its registry (and the provenance of its latest version, PyPI also
/// of a few unchecked versions), asks OSV.dev once for the advisories of every latest version, merges with
/// <see cref="RegistrySyncRules"/>, saves the rows in bulk and audits and notifies the changes. A package the registry
/// cannot be read for keeps its rows and gets a sync error.
/// </summary>
/// <param name="reader"></param>
/// <param name="packageRepository"></param>
/// <param name="versionRepository"></param>
/// <param name="alertRepository"></param>
/// <param name="storeJobQueue"></param>
/// <param name="featureFlagService"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class RegistrySyncer(IPackageRegistryReader reader,
                                   IStoreasyRepositoryBase<TrackedPackage> packageRepository,
                                   IStoreasyRepositoryBase<TrackedPackageVersion> versionRepository,
                                   IStoreasyRepositoryBase<PackageAlert> alertRepository,
                                   IStoreJobQueue storeJobQueue,
                                   IFeatureFlagService featureFlagService,
                                   IAuditTrail auditTrail,
                                   INotificationDispatcher notificationDispatcher,
                                   RegistryOptions options,
                                   TimeProvider timeProvider) : IRegistrySyncer
{
    private readonly IPackageRegistryReader _reader = reader;
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<TrackedPackageVersion> _versionRepository = versionRepository;
    private readonly IStoreasyRepositoryBase<PackageAlert> _alertRepository = alertRepository;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IFeatureFlagService _featureFlagService = featureFlagService;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly RegistryOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    private sealed record PackageRead(TrackedPackage Package, RegistryPackageRead Read, string Workflow);

    /// <inheritdoc/>
    public async Task<RegistrySyncSummary> SyncAsync(int organizationId, int? trackedPackageId, bool dueOnly, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = DueCutoff(now);
        var packages = await _packageRepository.GetAllAsync(p => p.OrganizationId == organizationId
                                                                  && (trackedPackageId == null || p.Id == trackedPackageId)
                                                                  && (!dueOnly || p.SyncedDate == null || p.SyncedDate < cutoff),
                                                             cancellationToken: cancellationToken);

        if (packages.Count == 0)
            return new RegistrySyncSummary(0, [], 0);

        var ids = packages.Select(p => p.Id).ToList();
        var versions = (await _versionRepository.GetAllAsync(v => ids.Contains(v.TrackedPackageId) && v.OrganizationId == organizationId, cancellationToken: cancellationToken))
                       .ToLookup(v => v.TrackedPackageId);
        var openAlerts = (await _alertRepository.GetAllAsync(a => ids.Contains(a.TrackedPackageId) && a.OrganizationId == organizationId && a.ResolvedDate == null, cancellationToken: cancellationToken))
                         .ToLookup(a => a.TrackedPackageId);

        var reads = new List<PackageRead>();
        var failed = new List<string>();

        foreach (var package in packages)
        {
            var read = await ReadAsync(package, versions[package.Id].ToList(), cancellationToken);

            if (read == null)
                failed.Add($"{package.Registry}:{package.Name}");
            else
                reads.Add(read);
        }

        var advisories = await ReadAdvisoriesAsync(reads, cancellationToken);
        var outcomes = new List<(TrackedPackage Package, RegistrySyncOutcome Outcome)>();

        for (var i = 0; i < reads.Count; i++)
        {
            var (package, read, workflow) = reads[i];
            var outcome = RegistrySyncRules.Merge(package, read, workflow, [.. versions[package.Id]], [.. openAlerts[package.Id]], advisories?[i], now, _options.MaxVersions);

            package.SyncedDate = now;
            package.SyncError = null;
            outcomes.Add((package, outcome));
        }

        await SaveAsync(packages, outcomes, cancellationToken);
        await ReportAsync(outcomes, cancellationToken);

        return new RegistrySyncSummary(reads.Count, failed, outcomes.Sum(o => o.Outcome.Changes.Count));
    }

    /// <inheritdoc/>
    public async Task<int> QueueDueAsync(CancellationToken cancellationToken)
    {
        if (!await _featureFlagService.IsEnabledAsync(FeatureFlagKeys.Registries, cancellationToken))
            return 0;

        var cutoff = DueCutoff(_timeProvider.GetUtcNow().UtcDateTime);
        var organizations = (await _packageRepository.GetAllAsync(p => p.SyncedDate == null || p.SyncedDate < cutoff, projection: p => new TrackedPackage { OrganizationId = p.OrganizationId }, cancellationToken: cancellationToken))
                            .Select(p => p.OrganizationId)
                            .Distinct()
                            .ToList();

        if (organizations.Count == 0)
            return 0;

        var jobs = await _storeJobQueue.EnqueueAsync([.. organizations.Select(o => new StoreJobRequest(o, StoreJobKind.RegistrySync, new RegistrySyncPayload(null, DueOnly: true)))], cancellationToken);

        return jobs.Count;
    }

    // A few minutes of slack, so an hourly schedule does not skip a package synced a little less than the interval ago.
    private DateTime DueCutoff(DateTime now) => now.AddHours(-Math.Max(1, _options.SyncIntervalHours)).AddMinutes(10);

    /// <summary>
    /// Reads one package with its provenance; null (and the package's sync error set) when the registry could not be read.
    /// </summary>
    private async Task<PackageRead> ReadAsync(TrackedPackage package, List<TrackedPackageVersion> known, CancellationToken cancellationToken)
    {
        try
        {
            var read = await _reader.ReadPackageAsync(package.Registry, package.Name, cancellationToken);

            if (read == null)
            {
                package.SyncError = $"{RegistryNames.DisplayName(package.Registry)} has no package {package.Name} (any more).";
                return null;
            }

            var provenance = await ReadProvenanceAsync(package, read, known, cancellationToken);

            RegistrySyncRules.ApplyProvenance(read, provenance);

            return new PackageRead(package, read, read.LatestVersion != null ? provenance.GetValueOrDefault(read.LatestVersion)?.Workflow : null);
        }
        catch (StoreCallException ex)
        {
            package.SyncError = SecretRedactor.RedactAndTrim(ex.Message, TrackedPackage.SyncErrorMaxLength);
            return null;
        }
    }

    /// <summary>
    /// The provenance reads of a package: the latest version (npm only when the packument says it has one), and for PyPI up
    /// to <see cref="RegistryOptions.ProvenanceChecksPerSync"/> versions never checked, newest first. A failed provenance read
    /// leaves the version unchecked.
    /// </summary>
    private async Task<Dictionary<string, ProvenanceRead>> ReadProvenanceAsync(TrackedPackage package, RegistryPackageRead read, List<TrackedPackageVersion> known, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ProvenanceRead>(StringComparer.Ordinal);
        var latest = read.Latest;

        if (latest == null || !RegistryNames.SupportsProvenance(package.Registry))
            return result;

        var targets = new List<RegistryVersionRead>();

        if (package.Registry != PackageRegistry.Npm || latest.HasProvenance == true)
            targets.Add(latest);

        if (package.Registry == PackageRegistry.PyPI)
        {
            var checkedVersions = known.Where(v => v.HasProvenance != null).Select(v => v.Version).ToHashSet(StringComparer.Ordinal);

            targets.AddRange(read.Versions.Where(v => v != latest && !checkedVersions.Contains(v.Version) && v.ArtifactFile != null)
                                          .OrderByDescending(v => v.Published ?? DateTime.MinValue)
                                          .Take(Math.Max(0, _options.ProvenanceChecksPerSync)));
        }

        foreach (var version in targets)
        {
            try
            {
                var provenance = await _reader.ReadProvenanceAsync(package.Registry, package.Name, version, cancellationToken);

                // npm: the packument already said the version has provenance; an unreadable statement keeps that flag.
                if (provenance != null || package.Registry != PackageRegistry.Npm)
                    result[version.Version] = provenance;
            }
            catch (StoreCallException)
            {
            }
        }

        return result;
    }

    /// <summary>
    /// The advisories of every read package's latest version (one OSV.dev batch); null when OSV.dev could not be read, so the
    /// open advisory alerts stay as they are.
    /// </summary>
    private async Task<List<List<RegistryAdvisory>>> ReadAdvisoriesAsync(List<PackageRead> reads, CancellationToken cancellationToken)
    {
        var asked = reads.Select((r, index) => (Index: index, Query: new AdvisoryQuery(r.Package.Registry, r.Read.Name ?? r.Package.Name, r.Read.LatestVersion)))
                         .Where(q => !string.IsNullOrEmpty(q.Query.Version))
                         .ToList();
        var result = reads.Select(_ => new List<RegistryAdvisory>()).ToList();

        if (asked.Count == 0)
            return result;

        try
        {
            var advisories = await _reader.ReadAdvisoriesAsync([.. asked.Select(q => q.Query)], cancellationToken);

            if (advisories.Count != asked.Count)
                return null;

            for (var i = 0; i < asked.Count; i++)
                result[asked[i].Index] = advisories[i];

            return result;
        }
        catch (StoreCallException)
        {
            return null;
        }
    }

    private async Task SaveAsync(List<TrackedPackage> packages, List<(TrackedPackage Package, RegistrySyncOutcome Outcome)> outcomes, CancellationToken cancellationToken)
    {
        await _packageRepository.UpdateAsync(packages, cancellationToken);

        var added = outcomes.SelectMany(o => o.Outcome.AddedVersions).ToList();
        var updated = outcomes.SelectMany(o => o.Outcome.UpdatedVersions).ToList();
        var opened = outcomes.SelectMany(o => o.Outcome.OpenedAlerts).ToList();
        var resolved = outcomes.SelectMany(o => o.Outcome.ResolvedAlerts).ToList();

        if (added.Count > 0)
            await _versionRepository.AddRangeAsync(added, cancellationToken);

        if (updated.Count > 0)
            await _versionRepository.UpdateAsync(updated, cancellationToken);

        if (opened.Count > 0)
            await _alertRepository.AddRangeAsync(opened, cancellationToken);

        if (resolved.Count > 0)
            await _alertRepository.UpdateAsync(resolved, cancellationToken);
    }

    private async Task ReportAsync(List<(TrackedPackage Package, RegistrySyncOutcome Outcome)> outcomes, CancellationToken cancellationToken)
    {
        var changes = outcomes.SelectMany(o => o.Outcome.Changes.Select(c => (o.Package, Change: c))).ToList();

        if (changes.Count == 0)
            return;

        _auditTrail.Channel = ActionChannel.System;

        await _auditTrail.WriteManyAsync([.. changes.Select(c => new AuditEntry(c.Package.OrganizationId,
                                                                                ActionOf(c.Change.Kind),
                                                                                c.Package.Id.ToString(CultureInfo.InvariantCulture),
                                                                                $"{RegistryNames.DisplayName(c.Package.Registry)} {c.Package.Name} {c.Change.Version}: {c.Change.Kind}{(string.IsNullOrEmpty(c.Change.Detail) ? string.Empty : " (" + c.Change.Detail + ")")}.",
                                                                                Details: new { c.Package.Registry, c.Package.Name, c.Change.Version, Change = c.Change.Kind.ToString(), c.Change.Detail },
                                                                                UserName: "system",
                                                                                Channel: ActionChannel.System))],
                                         cancellationToken);

        foreach (var (package, change) in changes)
            await _notificationDispatcher.DispatchAsync(Notification(package, change), cancellationToken);
    }

    private static OrgNotification Notification(TrackedPackage package, RegistryChange change) => new()
    {
        OrganizationId = package.OrganizationId,
        Kind = NotificationType.PackageRegistry,
        Roles = NotificationAudiences.Editors,
        TextKey = change.Kind switch
        {
            RegistryChangeKind.NewVersion => MessageKey.NotificationRegistryNewVersion,
            RegistryChangeKind.Deprecated => MessageKey.NotificationRegistryDeprecated,
            RegistryChangeKind.Yanked => MessageKey.NotificationRegistryYanked,
            _ => MessageKey.NotificationRegistryAdvisory,
        },
        TextArgs = [package.Name, RegistryNames.DisplayName(package.Registry), change.Version, change.Detail ?? string.Empty],
        Data = new { trackedPackageId = package.Id, registry = package.Registry.ToString(), name = package.Name, version = change.Version, change = change.Kind.ToString() },
        AppId = package.AppId,
        RelatedEntity = NotificationEntity.TrackedPackage,
        RelatedEntityId = $"{package.Id}:{change.Kind}:{change.Version}",
        ActionLink = $"/registries/{package.Id}",
    };

    private static AuditAction ActionOf(RegistryChangeKind kind) => kind switch
    {
        RegistryChangeKind.NewVersion => AuditAction.RegistryVersionPublished,
        RegistryChangeKind.Deprecated => AuditAction.RegistryVersionDeprecated,
        RegistryChangeKind.Yanked => AuditAction.RegistryVersionYanked,
        _ => AuditAction.RegistryAdvisoryFound,
    };
}
