using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Registries;

/// <summary>
/// A package the organization watches on a package registry (npm, NuGet, PyPI, crates.io): what the last sync read from
/// the registry's public API. storeasy does not publish packages and stores no registry token; publishing stays in CI.
/// </summary>
[Table(TableNames.TrackedPackages)]
[Index(nameof(OrganizationId), nameof(Registry), nameof(Name), IsUnique = true)]
[Index(nameof(SyncedDate))]
public class TrackedPackage : AuditableEntity<int>
{
    /// <summary>
    /// Maximum length of a package name (npm allows 214 characters, the longest of the four registries).
    /// </summary>
    public const int NameMaxLength = 214;

    /// <summary>
    /// Maximum length of a version string.
    /// </summary>
    public const int VersionMaxLength = 128;

    /// <summary>
    /// Maximum length of an address (repository, CI run).
    /// </summary>
    public const int UrlMaxLength = 500;

    /// <summary>
    /// Maximum length of a deprecation message.
    /// </summary>
    public const int MessageMaxLength = 500;

    /// <summary>
    /// Maximum length of <see cref="SyncError"/>.
    /// </summary>
    public const int SyncErrorMaxLength = 500;

    /// <summary>
    /// Maximum length of <see cref="SetupRepository"/>.
    /// </summary>
    public const int SetupRepositoryMaxLength = 200;

    /// <summary>
    /// Maximum length of <see cref="SetupWorkflow"/>.
    /// </summary>
    public const int SetupWorkflowMaxLength = 200;

    /// <summary>
    /// Maximum length of <see cref="SetupEnvironment"/> and <see cref="SetupRegistryUser"/>.
    /// </summary>
    public const int SetupEnvironmentMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The registry.
    /// </summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>
    /// Package name as the registry spells it (npm names may be scoped, <c>@scope/name</c>).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// storeasy app the package belongs to, when a member linked one.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// The registry's latest version (npm <c>dist-tags.latest</c>, crates.io <c>max_stable_version</c>, PyPI
    /// <c>info.version</c>, NuGet the highest listed version).
    /// </summary>
    [MaxLength(VersionMaxLength)]
    public string LatestVersion { get; set; }

    /// <summary>
    /// UTC instant the latest version was published.
    /// </summary>
    public DateTime? LatestPublishedDate { get; set; }

    /// <summary>
    /// Source repository: from the latest version's provenance when it has one, otherwise the repository the package declares.
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string SourceRepository { get; set; }

    /// <summary>
    /// CI run that published the latest version (from its provenance); null without provenance or when the registry does not
    /// name the run (PyPI names the workflow only).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string LatestCiRunUrl { get; set; }

    /// <summary>
    /// CI workflow that published the latest version (from its provenance), e.g. <c>.github/workflows/release.yml</c>.
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string LatestWorkflow { get; set; }

    /// <summary>
    /// Whether the latest version has provenance (always false for NuGet, which has none).
    /// </summary>
    public bool HasProvenance { get; set; }

    /// <summary>
    /// Deprecation message of the latest version; null when it is not deprecated.
    /// </summary>
    [MaxLength(MessageMaxLength)]
    public string DeprecationMessage { get; set; }

    /// <summary>
    /// UTC instant of the last successful sync; null until the first one.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Why the last sync could not read the package; null after a successful one.
    /// </summary>
    [MaxLength(SyncErrorMaxLength)]
    public string SyncError { get; set; }

    /// <summary>
    /// CI provider of the trusted publishing setup; null while the package has no setup.
    /// </summary>
    public CiProvider? SetupProvider { get; set; }

    /// <summary>
    /// Repository the setup expects releases from: <c>owner/repo</c> (GitHub) or <c>namespace/project</c> (GitLab).
    /// </summary>
    [MaxLength(SetupRepositoryMaxLength)]
    public string SetupRepository { get; set; }

    /// <summary>
    /// Workflow file the setup expects: a file name in <c>.github/workflows</c> (GitHub) or the top-level CI file path
    /// (GitLab).
    /// </summary>
    [MaxLength(SetupWorkflowMaxLength)]
    public string SetupWorkflow { get; set; }

    /// <summary>
    /// CI environment of the setup, when the registry setting names one.
    /// </summary>
    [MaxLength(SetupEnvironmentMaxLength)]
    public string SetupEnvironment { get; set; }

    /// <summary>
    /// Registry user the CI logs in as, where the registry asks for one (NuGet's <c>NuGet/login</c> action).
    /// </summary>
    [MaxLength(SetupEnvironmentMaxLength)]
    public string SetupRegistryUser { get; set; }

    /// <summary>
    /// UTC instant the setup was saved.
    /// </summary>
    public DateTime? SetupDate { get; set; }

    /// <summary>
    /// How a release of the package starts in its repository's CI; null until set (a group's settings win for a member).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public ReleaseSettings Release { get; set; }

    /// <summary>
    /// Group the package is released with.
    /// </summary>
    public int? PackageGroupId { get; set; }

    /// <summary>
    /// All time downloads; null where the registry gives no total (PyPI) or none at all (Maven Central).
    /// </summary>
    public long? TotalDownloads { get; set; }

    /// <summary>
    /// Downloads in the last <see cref="RecentDownloadsDays"/> days, as the registry counts them.
    /// </summary>
    public long? RecentDownloads { get; set; }

    /// <summary>
    /// Window of <see cref="RecentDownloads"/> in days (30 on npm, PyPI, Packagist and pub.dev, 90 on crates.io).
    /// </summary>
    public int? RecentDownloadsDays { get; set; }

    /// <summary>
    /// What the per version download counts cover; null where the registry gives none.
    /// </summary>
    public DownloadScope? VersionDownloadsScope { get; set; }

    /// <summary>
    /// Likes (pub.dev) or favers (Packagist).
    /// </summary>
    public long? Likes { get; set; }

    /// <summary>
    /// pub.dev points granted.
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    /// pub.dev points possible.
    /// </summary>
    public int? MaxPoints { get; set; }

    /// <summary>
    /// UTC instant the downloads were last read.
    /// </summary>
    public DateTime? DownloadsSyncedDate { get; set; }

    /// <summary>
    /// The group.
    /// </summary>
    [ForeignKey(nameof(PackageGroupId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual PackageGroup PackageGroup { get; set; }

    /// <summary>
    /// The linked app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual App App { get; set; }

    /// <summary>
    /// Versions the registry lists.
    /// </summary>
    public virtual List<TrackedPackageVersion> Versions { get; set; }

    /// <summary>
    /// Alerts, open and resolved.
    /// </summary>
    public virtual List<PackageAlert> Alerts { get; set; }
}
