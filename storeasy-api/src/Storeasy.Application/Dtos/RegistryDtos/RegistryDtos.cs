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
