using Storeasy.Application.Utils.Registries;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads the public, anonymous APIs of the package registries (npm, NuGet, PyPI, crates.io) and OSV.dev. A failed call
/// throws <see cref="StoreCallException"/> (transient for timeouts, 429 and 5xx answers).
/// </summary>
public interface IPackageRegistryReader
{
    /// <summary>
    /// Reads a package with every version; null when the registry does not know it.
    /// </summary>
    Task<RegistryPackageRead> ReadPackageAsync(PackageRegistry registry, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the provenance of one version; null when it has none (npm reads the attestations endpoint, PyPI the Integrity
    /// API for <see cref="RegistryVersionRead.ArtifactFile"/>; crates.io names it in the version list and NuGet has none).
    /// </summary>
    Task<ProvenanceRead> ReadProvenanceAsync(PackageRegistry registry, string name, RegistryVersionRead version, CancellationToken cancellationToken);

    /// <summary>
    /// The OSV.dev advisories of each query, in the query order (one batch query and the details of up to
    /// <c>MaxAdvisoryDetails</c> advisories).
    /// </summary>
    Task<List<List<RegistryAdvisory>>> ReadAdvisoriesAsync(IReadOnlyList<AdvisoryQuery> queries, CancellationToken cancellationToken);
}

/// <summary>
/// Syncs tracked packages: reads them from their registries, saves versions and alerts, audits and notifies the changes.
/// </summary>
public interface IRegistrySyncer
{
    /// <summary>
    /// Syncs the tracked packages of <paramref name="organizationId"/>, or only <paramref name="trackedPackageId"/>;
    /// <paramref name="dueOnly"/> skips the packages synced within the interval.
    /// </summary>
    Task<RegistrySyncSummary> SyncAsync(int organizationId, int? trackedPackageId, bool dueOnly, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a <see cref="StoreJobKind.RegistrySync"/> job for every organization with a package not synced within the
    /// interval (nothing while the module is switched off); returns the number of jobs.
    /// </summary>
    Task<int> QueueDueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Token expiry reminders: opens and resolves <see cref="PackageAlertKind.TokenExpiring"/> alerts and warns 14 and 3 days
/// before and on the expiry date (audit entry and notification). Tokens are never stored.
/// </summary>
public interface IRegistryReminderChecker
{
    /// <summary>
    /// Checks the reminders of <paramref name="organizationId"/>, or of every organization (the daily schedule, nothing
    /// while the module is switched off); returns the number of warnings sent.
    /// </summary>
    Task<int> CheckAsync(int? organizationId, CancellationToken cancellationToken);
}

/// <summary>
/// Result of <see cref="IRegistrySyncer.SyncAsync"/>.
/// </summary>
/// <param name="Read">Packages read.</param>
/// <param name="Failed">Names of the packages that could not be read.</param>
/// <param name="Changes">Changes reported.</param>
public sealed record RegistrySyncSummary(int Read, List<string> Failed, int Changes);
