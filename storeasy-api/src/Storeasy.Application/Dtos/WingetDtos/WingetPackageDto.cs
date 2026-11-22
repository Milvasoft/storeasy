using Storeasy.Domain.Winget;
using System.Linq.Expressions;

namespace Storeasy.Application.Dtos.WingetDtos;

/// <summary>
/// A winget package storeasy tracks: what the last sync read from winget-pkgs, its pull requests and the installers
/// storeasy already knows for the same app (to prefill a new version).
/// </summary>
public class WingetPackageDto
{
    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>winget connection.</summary>
    public int? ConnectionId { get; set; }

    /// <summary>Fork the pull requests come from (<c>owner/name</c>).</summary>
    public string Fork { get; set; }

    /// <summary>Package identifier.</summary>
    public string PackageIdentifier { get; set; }

    /// <summary>Whether winget-pkgs has a version of the package.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Highest version in winget-pkgs.</summary>
    public string LatestVersion { get; set; }

    /// <summary>Every version in winget-pkgs, highest first.</summary>
    public List<string> Versions { get; set; } = [];

    /// <summary>Publisher of the latest version.</summary>
    public string Publisher { get; set; }

    /// <summary>Package name of the latest version.</summary>
    public string PackageName { get; set; }

    /// <summary>Default locale.</summary>
    public string DefaultLocale { get; set; }

    /// <summary>Locales of the latest version.</summary>
    public List<string> Locales { get; set; } = [];

    /// <summary>Schema version of the latest manifests.</summary>
    public string ManifestVersion { get; set; }

    /// <summary>Schema version storeasy writes.</summary>
    public string WrittenManifestVersion { get; set; }

    /// <summary>Last successful sync.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>Why the last sync failed.</summary>
    public string SyncError { get; set; }

    /// <summary>Package folder in winget-pkgs.</summary>
    public string PackagePageUrl { get; set; }

    /// <summary>Pull requests, newest first.</summary>
    public List<WingetPullRequestDto> PullRequests { get; set; } = [];

    /// <summary>MSI/EXE installers of the app's Microsoft Store product a new version can start from.</summary>
    public List<WingetInstallerSourceDto> InstallerSources { get; set; } = [];

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<WingetPackage, WingetPackageDto>> Projection { get; } = p => new WingetPackageDto
    {
        StoreAppId = p.StoreAppId,
        AppId = p.StoreApp.AppId,
        AppName = p.StoreApp.App.Name,
        ConnectionId = p.StoreApp.StoreConnectionId,
        PackageIdentifier = p.PackageIdentifier,
        IsPublished = p.IsPublished,
        LatestVersion = p.LatestVersion,
        Versions = p.Versions,
        Publisher = p.Publisher,
        PackageName = p.PackageName,
        DefaultLocale = p.DefaultLocale,
        Locales = p.Locales,
        ManifestVersion = p.ManifestVersion,
        SyncedDate = p.SyncedDate,
        SyncError = p.SyncError,
    };
}

/// <summary>
/// An installer storeasy knows from the app's Microsoft Store MSI/EXE product.
/// </summary>
public class WingetInstallerSourceDto
{
    /// <summary>Download address.</summary>
    public string Url { get; set; }

    /// <summary>winget installer type (<c>msi</c> or <c>exe</c>).</summary>
    public string Type { get; set; }

    /// <summary>winget architectures.</summary>
    public List<string> Architectures { get; set; } = [];

    /// <summary>Silent switches (EXE).</summary>
    public string SilentSwitches { get; set; }

    /// <summary>Locales of the installer.</summary>
    public List<string> Locales { get; set; } = [];

    /// <summary>Result of storeasy's Authenticode check.</summary>
    public MsSignatureStatus SignatureStatus { get; set; }
}

/// <summary>
/// A pull request to winget-pkgs and its review state.
/// </summary>
public class WingetPullRequestDto
{
    /// <summary>Row id.</summary>
    public long Id { get; set; }

    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Number in winget-pkgs.</summary>
    public int Number { get; set; }

    /// <summary>Page.</summary>
    public string Url { get; set; }

    /// <summary>Title.</summary>
    public string Title { get; set; }

    /// <summary>Package version.</summary>
    public string PackageVersion { get; set; }

    /// <summary>What it changes; null for one found by the sync.</summary>
    public WingetChangeKind? ChangeKind { get; set; }

    /// <summary>Review state.</summary>
    public WingetPullRequestState State { get; set; }

    /// <summary>Whether it is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>Whether the author must act (validation failed or feedback asked).</summary>
    public bool NeedsAction { get; set; }

    /// <summary>Labels.</summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>Check runs of the head commit.</summary>
    public List<WingetCheck> Checks { get; set; } = [];

    /// <summary>Newest bot and moderator comments.</summary>
    public List<WingetComment> Comments { get; set; } = [];

    /// <summary>States seen.</summary>
    public List<WingetStateChange> StatusHistory { get; set; } = [];

    /// <summary>Whether storeasy opened it.</summary>
    public bool CreatedByStoreasy { get; set; }

    /// <summary>The plan that opened it.</summary>
    public long? PlanId { get; set; }

    /// <summary>Opened.</summary>
    public DateTime? OpenedDate { get; set; }

    /// <summary>Merged (published).</summary>
    public DateTime? MergedDate { get; set; }

    /// <summary>Closed.</summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>Last sync.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Projection of the row.
    /// </summary>
    public static Expression<Func<WingetPullRequest, WingetPullRequestDto>> Projection { get; } = p => new WingetPullRequestDto
    {
        Id = p.Id,
        StoreAppId = p.StoreAppId,
        Number = p.Number,
        Url = p.Url,
        Title = p.Title,
        PackageVersion = p.PackageVersion,
        ChangeKind = p.ChangeKind,
        State = p.State,
        IsOpen = p.IsOpen,
        NeedsAction = p.State == WingetPullRequestState.ValidationFailed || p.State == WingetPullRequestState.NeedsAuthorFeedback,
        Labels = p.Labels,
        Checks = p.Checks,
        Comments = p.Comments,
        StatusHistory = p.StatusHistory,
        CreatedByStoreasy = p.CreatedByStoreasy,
        PlanId = p.PlanId,
        OpenedDate = p.OpenedDate,
        MergedDate = p.MergedDate,
        ClosedDate = p.ClosedDate,
        SyncedDate = p.SyncedDate,
    };
}

/// <summary>
/// The manifests of one version as read from winget-pkgs, with the listing texts per locale and storeasy's checks.
/// </summary>
public class WingetManifestDto
{
    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Package identifier.</summary>
    public string PackageIdentifier { get; set; }

    /// <summary>Version read.</summary>
    public string Version { get; set; }

    /// <summary>Every version in winget-pkgs, highest first.</summary>
    public List<string> Versions { get; set; } = [];

    /// <summary>Default locale.</summary>
    public string DefaultLocale { get; set; }

    /// <summary>Schema version.</summary>
    public string ManifestVersion { get; set; }

    /// <summary>Listing texts per locale, the default locale first (a list: locale tags are values, not keys).</summary>
    public List<WingetLocaleTextsDto> Locales { get; set; } = [];

    /// <summary>Installer entries.</summary>
    public List<WingetInstallerDto> Installers { get; set; } = [];

    /// <summary>Manifest files (name and YAML).</summary>
    public List<WingetFileDto> Files { get; set; } = [];

    /// <summary>storeasy's schema and policy checks.</summary>
    public List<WingetIssueDto> Issues { get; set; } = [];
}

/// <summary>
/// The listing texts of one locale manifest.
/// </summary>
public class WingetLocaleTextsDto
{
    /// <summary>Locale.</summary>
    public string Locale { get; set; }

    /// <summary>Whether it is the default locale.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Text fields: manifest key to value.</summary>
    public Dictionary<string, string> Fields { get; set; } = [];

    /// <summary>Tags.</summary>
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// One installer entry of an installer manifest.
/// </summary>
public class WingetInstallerDto
{
    /// <summary>Architecture.</summary>
    public string Architecture { get; set; }

    /// <summary>Installer type.</summary>
    public string Type { get; set; }

    /// <summary>Scope.</summary>
    public string Scope { get; set; }

    /// <summary>Download address.</summary>
    public string Url { get; set; }

    /// <summary>SHA-256.</summary>
    public string Sha256 { get; set; }

    /// <summary>Silent switches.</summary>
    public string SilentSwitches { get; set; }

    /// <summary>Silent with progress switches.</summary>
    public string SilentWithProgressSwitches { get; set; }

    /// <summary>MSI product code.</summary>
    public string ProductCode { get; set; }

    /// <summary>Installer locale.</summary>
    public string Locale { get; set; }
}

/// <summary>
/// A manifest file.
/// </summary>
public class WingetFileDto
{
    /// <summary>File name.</summary>
    public string FileName { get; set; }

    /// <summary>YAML text.</summary>
    public string Content { get; set; }
}

/// <summary>
/// A schema or policy problem, localized.
/// </summary>
public class WingetIssueDto
{
    /// <summary>Message key.</summary>
    public string Key { get; set; }

    /// <summary>Message.</summary>
    public string Message { get; set; }

    /// <summary>Manifest field.</summary>
    public string Field { get; set; }

    /// <summary>Locale.</summary>
    public string Locale { get; set; }

    /// <summary>Whether the validation pipeline would reject it.</summary>
    public bool Blocking { get; set; }
}

/// <summary>
/// The answer of adding a winget package.
/// </summary>
public class WingetPackageAddedDto
{
    /// <summary>storeasy app the package belongs to.</summary>
    public int AppId { get; set; }

    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Sync job reading the package.</summary>
    public long JobId { get; set; }
}
