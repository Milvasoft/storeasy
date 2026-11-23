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

    /// <summary>CI provider of the trusted publishing setup; null without one.</summary>
    public CiProvider? SetupProvider { get; set; }

    /// <summary>Repository of the trusted publishing setup.</summary>
    public string SetupRepository { get; set; }

    /// <summary>Workflow file of the trusted publishing setup.</summary>
    public string SetupWorkflow { get; set; }

    /// <summary>Whether the latest version came from the setup's repository and workflow.</summary>
    public PublisherCheckStatus PublisherCheck { get; set; }

    /// <summary>All time downloads.</summary>
    public long? TotalDownloads { get; set; }

    /// <summary>Downloads in the last <see cref="RecentDownloadsDays"/> days.</summary>
    public long? RecentDownloads { get; set; }

    /// <summary>Window of <see cref="RecentDownloads"/>.</summary>
    public int? RecentDownloadsDays { get; set; }

    /// <summary>Group the package is released with.</summary>
    public int? PackageGroupId { get; set; }

    /// <summary>Name of the group.</summary>
    public string PackageGroupName { get; set; }

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
        SetupProvider = p.SetupProvider,
        SetupRepository = p.SetupRepository,
        SetupWorkflow = p.SetupWorkflow,
        TotalDownloads = p.TotalDownloads,
        RecentDownloads = p.RecentDownloads,
        RecentDownloadsDays = p.RecentDownloadsDays,
        PackageGroupId = p.PackageGroupId,
        PackageGroupName = p.PackageGroup != null ? p.PackageGroup.Name : null,
    };

    /// <summary>
    /// Fills the values computed from the registry (registry page, provenance support).
    /// </summary>
    public TrackedPackageSummaryDto Complete()
    {
        RegistryUrl = RegistryNames.PageUrl(Registry, Name);
        ProvenanceSupported = RegistryNames.SupportsProvenance(Registry);
        PublisherCheck = TrustedPublishing.Check(new TrackedPackage
        {
            Registry = Registry,
            LatestVersion = LatestVersion,
            HasProvenance = HasProvenance,
            SourceRepository = SourceRepository,
            LatestWorkflow = LatestWorkflow,
            SetupProvider = SetupProvider,
            SetupRepository = SetupRepository,
            SetupWorkflow = SetupWorkflow,
        }).Status;

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

    /// <summary>Downloads as the registry counts them (all time, or the last 7 days on npm).</summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<TrackedPackageVersion, TrackedPackageVersionDto>> Projection { get; } = v => new TrackedPackageVersionDto
    {
        Downloads = v.Downloads,
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
    public int? TrackedPackageId { get; set; }

    /// <summary>Token reminder of a token expiry alert.</summary>
    public int? TokenReminderId { get; set; }

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
        TokenReminderId = a.TokenReminderId,
        Registry = a.TrackedPackage != null ? a.TrackedPackage.Registry : a.TokenReminder.Registry,
        PackageName = a.TrackedPackage != null ? a.TrackedPackage.Name : a.TokenReminder.Label,
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

/// <summary>
/// One value to fill in on the registry's trusted publisher settings.
/// </summary>
public class SetupFieldDto
{
    /// <summary>Field (<c>owner</c>, <c>repository</c>, <c>workflow</c>, <c>environment</c>, <c>namespace</c>, <c>project</c>,
    /// <c>pipelineFile</c>, <c>repositoryPath</c>, <c>tagPattern</c>).</summary>
    public string Key { get; set; }

    /// <summary>What to fill in.</summary>
    public string Value { get; set; }
}

/// <summary>
/// The trusted publishing wizard of a tracked package: the registry side, the CI file and the check of the latest release.
/// </summary>
public class TrustedPublishingDto
{
    /// <summary>Tracked package.</summary>
    public int TrackedPackageId { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Package name.</summary>
    public string PackageName { get; set; }

    /// <summary>CI providers the registry accepts trusted publishing from; empty when it has none.</summary>
    public List<CiProvider> Providers { get; set; } = [];

    /// <summary>Where the trusted publisher is set up on the registry.</summary>
    public string SettingsUrl { get; set; }

    /// <summary>Whether these are the saved values (false for a preview).</summary>
    public bool Saved { get; set; }

    /// <summary>CI provider of the setup (or the preview).</summary>
    public CiProvider? Provider { get; set; }

    /// <summary>Repository (<c>owner/repo</c> or <c>namespace/project</c>).</summary>
    public string Repository { get; set; }

    /// <summary>Workflow file name (GitHub) or top-level CI file path (GitLab).</summary>
    public string Workflow { get; set; }

    /// <summary>CI environment.</summary>
    public string Environment { get; set; }

    /// <summary>Registry user the CI logs in as (NuGet).</summary>
    public string RegistryUser { get; set; }

    /// <summary>UTC instant the setup was saved.</summary>
    public DateTime? SetupDate { get; set; }

    /// <summary>Step 1: what to fill in on the registry.</summary>
    public List<SetupFieldDto> RegistryFields { get; set; } = [];

    /// <summary>Step 2: the path of the CI file in the repository.</summary>
    public string WorkflowPath { get; set; }

    /// <summary>Step 2: the CI file (text to copy; storeasy never commits it).</summary>
    public string WorkflowText { get; set; }

    /// <summary>Step 3: the latest release against the saved setup.</summary>
    public PublisherCheckDto Check { get; set; }

    /// <summary>
    /// Builds the wizard of <paramref name="package"/> for the given setup values (the saved ones, or a preview).
    /// </summary>
    public static TrustedPublishingDto Build(TrackedPackage package, CiProvider? provider, string repository, string workflow, string environment, string registryUser, bool saved)
    {
        var dto = new TrustedPublishingDto
        {
            TrackedPackageId = package.Id,
            Registry = package.Registry,
            PackageName = package.Name,
            Providers = [.. TrustedPublishing.Providers(package.Registry)],
            SettingsUrl = TrustedPublishing.SettingsUrl(package.Registry, package.Name),
            Saved = saved,
            Provider = provider,
            Repository = repository,
            Workflow = workflow,
            Environment = environment,
            RegistryUser = registryUser,
            SetupDate = saved ? package.SetupDate : null,
        };

        if (provider is CiProvider ci && dto.Providers.Contains(ci))
        {
            dto.RegistryFields = [.. TrustedPublishing.RegistryFields(package.Registry, ci, repository, workflow, environment).Select(f => new SetupFieldDto { Key = f.Key, Value = f.Value })];
            dto.WorkflowPath = string.IsNullOrEmpty(workflow) ? null : TrustedPublishing.WorkflowPath(ci, workflow);
            dto.WorkflowText = TrustedPublishing.WorkflowText(package.Registry, ci, environment, registryUser);
        }

        var check = TrustedPublishing.Check(package);

        dto.Check = new PublisherCheckDto
        {
            Status = check.Status,
            ExpectedRepository = check.ExpectedRepository,
            ExpectedWorkflow = check.ExpectedWorkflow,
            ObservedRepository = check.ObservedRepository,
            ObservedWorkflow = check.ObservedWorkflow,
            Version = check.Version,
            CiRunUrl = package.HasProvenance ? package.LatestCiRunUrl : null,
        };

        return dto;
    }
}

/// <summary>
/// The latest release of a tracked package against its trusted publishing setup.
/// </summary>
public class PublisherCheckDto
{
    /// <summary>Outcome.</summary>
    public PublisherCheckStatus Status { get; set; }

    /// <summary>Repository address the setup expects.</summary>
    public string ExpectedRepository { get; set; }

    /// <summary>Workflow path the setup expects.</summary>
    public string ExpectedWorkflow { get; set; }

    /// <summary>Repository the latest version's provenance names.</summary>
    public string ObservedRepository { get; set; }

    /// <summary>Workflow the latest version's provenance names.</summary>
    public string ObservedWorkflow { get; set; }

    /// <summary>The latest version.</summary>
    public string Version { get; set; }

    /// <summary>CI run that published it.</summary>
    public string CiRunUrl { get; set; }
}

/// <summary>
/// A registry token reminder (never the token).
/// </summary>
public class TokenReminderDto
{
    /// <summary>Reminder.</summary>
    public int Id { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Label.</summary>
    public string Label { get; set; }

    /// <summary>Where the token is used.</summary>
    public string UsedIn { get; set; }

    /// <summary>Expiry day (UTC).</summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>Linked package.</summary>
    public int? TrackedPackageId { get; set; }

    /// <summary>Name of the linked package.</summary>
    public string PackageName { get; set; }

    /// <summary>Days left until the expiry (negative after it).</summary>
    public int DaysLeft { get; set; }

    /// <summary>Warning stage today: 0 none, 1 within 14 days, 2 within 3 days, 3 expired.</summary>
    public byte Stage { get; set; }

    /// <summary>Whether the registry offers trusted publishing, so the token can be replaced.</summary>
    public bool TrustedPublishingAvailable { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<RegistryTokenReminder, TokenReminderDto>> Projection { get; } = r => new TokenReminderDto
    {
        Id = r.Id,
        Registry = r.Registry,
        Label = r.Label,
        UsedIn = r.UsedIn,
        ExpiryDate = r.ExpiryDate,
        TrackedPackageId = r.TrackedPackageId,
        PackageName = r.TrackedPackage != null ? r.TrackedPackage.Name : null,
    };

    /// <summary>
    /// Fills the values computed for <paramref name="today"/>.
    /// </summary>
    public TokenReminderDto Complete(DateTime today)
    {
        DaysLeft = TokenReminderRules.DaysLeft(ExpiryDate, today);
        Stage = TokenReminderRules.Stage(ExpiryDate, today);
        TrustedPublishingAvailable = TrustedPublishing.Providers(Registry).Count > 0;

        return this;
    }
}

/// <summary>
/// One day of a package's downloads.
/// </summary>
public class DownloadDayDto
{
    /// <summary>UTC day.</summary>
    public DateTime Day { get; set; }

    /// <summary>Downloads on the day (the registry's count, or the growth of the all time total since the day before).</summary>
    public long Downloads { get; set; }
}

/// <summary>
/// Downloads of one version.
/// </summary>
public class VersionDownloadsDto
{
    /// <summary>Version.</summary>
    public string Version { get; set; }

    /// <summary>Downloads (see <see cref="PackageDownloadsDto.VersionScope"/>).</summary>
    public long Downloads { get; set; }
}

/// <summary>
/// Downloads and usage of a tracked package.
/// </summary>
public class PackageDownloadsDto
{
    /// <summary>Tracked package.</summary>
    public int TrackedPackageId { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Whether the registry publishes download counts (false for Maven Central).</summary>
    public bool Available { get; set; }

    /// <summary>All time downloads.</summary>
    public long? TotalDownloads { get; set; }

    /// <summary>Downloads in the last <see cref="RecentDownloadsDays"/> days.</summary>
    public long? RecentDownloads { get; set; }

    /// <summary>Window of <see cref="RecentDownloads"/>.</summary>
    public int? RecentDownloadsDays { get; set; }

    /// <summary>What the per version counts cover.</summary>
    public DownloadScope? VersionScope { get; set; }

    /// <summary>Likes (pub.dev) or favers (Packagist).</summary>
    public long? Likes { get; set; }

    /// <summary>pub.dev points granted.</summary>
    public int? Points { get; set; }

    /// <summary>pub.dev points possible.</summary>
    public int? MaxPoints { get; set; }

    /// <summary>UTC instant the downloads were last read.</summary>
    public DateTime? DownloadsSyncedDate { get; set; }

    /// <summary>Daily series, oldest first.</summary>
    public List<DownloadDayDto> Series { get; set; } = [];

    /// <summary>Downloads per version, most downloaded first.</summary>
    public List<VersionDownloadsDto> Versions { get; set; } = [];
}

/// <summary>
/// One readiness check of a listing.
/// </summary>
public class ListingCheckDto
{
    /// <summary>What is checked (<c>description</c>, <c>readme</c>, <c>license</c>, <c>repository</c>, <c>keywords</c>,
    /// <c>icon</c>).</summary>
    public string Key { get; set; }

    /// <summary>Result.</summary>
    public ListingCheckStatus Status { get; set; }
}

/// <summary>
/// A package's page on its registry, read live, with readiness checks.
/// </summary>
public class PackageListingDto
{
    /// <summary>Tracked package.</summary>
    public int TrackedPackageId { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Whether storeasy can read the registry's listing (false for Maven Central).</summary>
    public bool Available { get; set; }

    /// <summary>Why the listing could not be read, when it could not.</summary>
    public string ReadError { get; set; }

    /// <summary>Version the listing belongs to.</summary>
    public string Version { get; set; }

    /// <summary>Description.</summary>
    public string Description { get; set; }

    /// <summary>README as text (HTML reduced to its text; never rendered as HTML).</summary>
    public string Readme { get; set; }

    /// <summary>Format the registry served the README in.</summary>
    public ReadmeFormat? ReadmeFormat { get; set; }

    /// <summary>Keywords, tags or topics.</summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>License.</summary>
    public string License { get; set; }

    /// <summary>Home page.</summary>
    public string Homepage { get; set; }

    /// <summary>Source repository.</summary>
    public string Repository { get; set; }

    /// <summary>Authors.</summary>
    public List<string> Authors { get; set; } = [];

    /// <summary>Icon address.</summary>
    public string IconUrl { get; set; }

    /// <summary>Readiness checks.</summary>
    public List<ListingCheckDto> Checks { get; set; } = [];

    /// <summary>Whether the listing changes only with a new release (false for Packagist, which reads the repository).</summary>
    public bool ShipsWithRelease { get; set; }

    /// <summary>The package's page on its registry.</summary>
    public string RegistryUrl { get; set; }
}

/// <summary>
/// One member of a package group.
/// </summary>
public class PackageGroupMemberDto
{
    /// <summary>Tracked package.</summary>
    public int TrackedPackageId { get; set; }

    /// <summary>Registry.</summary>
    public PackageRegistry Registry { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Latest version.</summary>
    public string LatestVersion { get; set; }

    /// <summary>UTC instant the latest version was published.</summary>
    public DateTime? LatestPublishedDate { get; set; }

    /// <summary>Whether the latest version is below the group's highest version.</summary>
    public bool Behind { get; set; }

    /// <summary>Open alerts.</summary>
    public int OpenAlertCount { get; set; }
}

/// <summary>
/// A package group with its members.
/// </summary>
public class PackageGroupDto
{
    /// <summary>Group.</summary>
    public int Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; }

    /// <summary>Highest latest version among the members.</summary>
    public string HighestVersion { get; set; }

    /// <summary>Members behind <see cref="HighestVersion"/>.</summary>
    public int BehindCount { get; set; }

    /// <summary>Most recent release among the members.</summary>
    public DateTime? LastReleaseDate { get; set; }

    /// <summary>Open alerts of all members.</summary>
    public int OpenAlertCount { get; set; }

    /// <summary>Members, behind first, then by name.</summary>
    public List<PackageGroupMemberDto> Members { get; set; } = [];

    /// <summary>
    /// Builds the group from its members' rows.
    /// </summary>
    public static PackageGroupDto From(int id, string name, IEnumerable<TrackedPackageSummaryDto> members)
    {
        var list = members.ToList();
        var highest = PackageGroups.Highest(list.Select(m => m.LatestVersion));
        var rows = list.Select(m => new PackageGroupMemberDto
        {
            TrackedPackageId = m.Id,
            Registry = m.Registry,
            Name = m.Name,
            LatestVersion = m.LatestVersion,
            LatestPublishedDate = m.LatestPublishedDate,
            Behind = highest != null && PackageGroups.Compare(m.LatestVersion, highest) < 0,
            OpenAlertCount = m.OpenAlertCount,
        }).OrderByDescending(m => m.Behind).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();

        return new PackageGroupDto
        {
            Id = id,
            Name = name,
            HighestVersion = highest,
            BehindCount = rows.Count(r => r.Behind),
            LastReleaseDate = rows.Max(r => r.LatestPublishedDate),
            OpenAlertCount = rows.Sum(r => r.OpenAlertCount),
            Members = rows,
        };
    }
}

/// <summary>
/// A suggested group of packages from one source repository.
/// </summary>
public class PackageGroupSuggestionDto
{
    /// <summary>Suggested name (<c>owner/repo</c>).</summary>
    public string Name { get; set; }

    /// <summary>The shared repository.</summary>
    public string Repository { get; set; }

    /// <summary>Members.</summary>
    public List<int> TrackedPackageIds { get; set; } = [];

    /// <summary>Names of the members.</summary>
    public List<string> PackageNames { get; set; } = [];
}
