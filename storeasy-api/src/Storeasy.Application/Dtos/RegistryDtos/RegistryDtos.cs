using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.RegistryDtos;

/// <summary>
/// Payload of <see cref="StoreJobKind.RegistrySync"/>.
/// </summary>
/// <param name="TrackedPackageId">One package only; null for every package of the organization.</param>
/// <param name="DueOnly">Only packages not synced within the interval (the schedule).</param>
public sealed record RegistrySyncPayload(int? TrackedPackageId = null, bool DueOnly = false);

/// <summary>
/// One row of the package summary.
/// </summary>
public class TrackedPackageSummaryDto
{
    /// <summary>Tracked package.</summary>
    public int Id { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Package name.</summary>
    public string Name { get; set; }

    /// <summary>Linked storeasy app.</summary>
    public int? AppId { get; set; }

    /// <summary>Name of the linked app.</summary>
    public string AppName { get; set; }

    /// <summary>Latest version.</summary>
    public string LatestVersion { get; set; }

    /// <summary>UTC instant the latest version was published.</summary>
    public DateTime? LatestPublishedDate { get; set; }

    /// <summary>CI run that published the latest version (from its provenance).</summary>
    public string LatestCiRunUrl { get; set; }

    /// <summary>CI workflow that published the latest version.</summary>
    public string LatestWorkflow { get; set; }

    /// <summary>Source repository.</summary>
    public string SourceRepository { get; set; }

    /// <summary>Whether the latest version has provenance.</summary>
    public bool HasProvenance { get; set; }

    /// <summary>Whether the registry records provenance at all (false for NuGet).</summary>
    public bool ProvenanceSupported { get; set; }

    /// <summary>Deprecation message of the latest version.</summary>
    public string DeprecationMessage { get; set; }

    /// <summary>Open alerts.</summary>
    public int OpenAlertCount { get; set; }

    /// <summary>UTC instant of the last successful sync.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>Why the last sync failed.</summary>
    public string SyncError { get; set; }

    /// <summary>The package's page on its registry.</summary>
    public string RegistryUrl { get; set; }

    /// <summary>
    /// Projection of the row (the linked app only while it is not deleted).
    /// </summary>
    public static Expression<Func<TrackedPackage, TrackedPackageSummaryDto>> Projection { get; } = p => new TrackedPackageSummaryDto
    {
        Id = p.Id,
        Registry = p.Registry,
        Name = p.Name,
        AppId = p.App != null && !p.App.IsDeleted ? p.AppId : null,
        AppName = p.App != null && !p.App.IsDeleted ? p.App.Name : null,
        LatestVersion = p.LatestVersion,
        LatestPublishedDate = p.LatestPublishedDate,
        LatestCiRunUrl = p.LatestCiRunUrl,
        LatestWorkflow = p.LatestWorkflow,
        SourceRepository = p.SourceRepository,
        HasProvenance = p.HasProvenance,
        DeprecationMessage = p.DeprecationMessage,
        OpenAlertCount = p.Alerts.Count(a => a.ResolvedDate == null),
        SyncedDate = p.SyncedDate,
        SyncError = p.SyncError,
    };

    /// <summary>
    /// Fills the values computed from the registry (registry page, provenance support).
    /// </summary>
    public TrackedPackageSummaryDto Complete()
    {
        RegistryUrl = RegistryNames.PageUrl(Registry, Name);
        ProvenanceSupported = RegistryNames.SupportsProvenance(Registry);

        return this;
    }
}

/// <summary>
/// A tracked package with its versions and alerts.
/// </summary>
public class TrackedPackageDto : TrackedPackageSummaryDto
{
    /// <summary>Versions, newest first.</summary>
    public List<TrackedPackageVersionDto> Versions { get; set; } = [];

    /// <summary>Alerts, open first, then newest first.</summary>
    public List<PackageAlertDto> Alerts { get; set; } = [];
}

/// <summary>
/// One version of a tracked package.
/// </summary>
public class TrackedPackageVersionDto
{
    /// <summary>Version.</summary>
    public string Version { get; set; }

    /// <summary>UTC instant it was published.</summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>Yanked.</summary>
    public bool IsYanked { get; set; }

    /// <summary>Unlisted (NuGet).</summary>
    public bool IsUnlisted { get; set; }

    /// <summary>Deprecated.</summary>
    public bool IsDeprecated { get; set; }

    /// <summary>Deprecation message or yank reason.</summary>
    public string Message { get; set; }

    /// <summary>Provenance; null when not checked.</summary>
    public bool? HasProvenance { get; set; }

    /// <summary>CI run that published it.</summary>
    public string CiRunUrl { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<TrackedPackageVersion, TrackedPackageVersionDto>> Projection { get; } = v => new TrackedPackageVersionDto
    {
        Version = v.Version,
        PublishedDate = v.PublishedDate,
        IsYanked = v.IsYanked,
        IsUnlisted = v.IsUnlisted,
        IsDeprecated = v.IsDeprecated,
        Message = v.Message,
        HasProvenance = v.HasProvenance,
        CiRunUrl = v.CiRunUrl,
    };
}

/// <summary>
/// An alert of a tracked package.
/// </summary>
public class PackageAlertDto
{
    /// <summary>Alert.</summary>
    public long Id { get; set; }

    /// <summary>Tracked package.</summary>
    public int TrackedPackageId { get; set; }

    /// <summary>Registry of the package.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Package name.</summary>
    public string PackageName { get; set; }

    /// <summary>Kind.</summary>
    public PackageAlertKind Kind { get; set; }

    /// <summary>Version.</summary>
    public string Version { get; set; }

    /// <summary>Advisory id.</summary>
    public string AdvisoryId { get; set; }

    /// <summary>Advisory severity.</summary>
    public string Severity { get; set; }

    /// <summary>One line.</summary>
    public string Summary { get; set; }

    /// <summary>Details address.</summary>
    public string Url { get; set; }

    /// <summary>UTC instant opened.</summary>
    public DateTime OpenedDate { get; set; }

    /// <summary>UTC instant resolved; null while open.</summary>
    public DateTime? ResolvedDate { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<PackageAlert, PackageAlertDto>> Projection { get; } = a => new PackageAlertDto
    {
        Id = a.Id,
        TrackedPackageId = a.TrackedPackageId,
        Registry = a.TrackedPackage.Registry,
        PackageName = a.TrackedPackage.Name,
        Kind = a.Kind,
        Version = a.Version,
        AdvisoryId = a.AdvisoryId,
        Severity = a.Severity,
        Summary = a.Summary,
        Url = a.Url,
        OpenedDate = a.OpenedDate,
        ResolvedDate = a.ResolvedDate,
    };

    /// <summary>
    /// Open alerts first, then the newest.
    /// </summary>
    public static List<PackageAlertDto> Order(IEnumerable<PackageAlertDto> alerts)
        => [.. alerts.OrderBy(a => a.ResolvedDate != null).ThenByDescending(a => a.ResolvedDate ?? a.OpenedDate).ThenByDescending(a => a.Id)];
}

/// <summary>
/// The answer of adding a package.
/// </summary>
public class TrackedPackageAddedDto
{
    /// <summary>Tracked package.</summary>
    public int Id { get; set; }

    /// <summary>Package name as the registry spells it.</summary>
    public string Name { get; set; }

    /// <summary>Sync job reading the package.</summary>
    public long JobId { get; set; }
}
