namespace Storeasy.Application.Dtos.FirefoxDtos;

/// <summary>
/// Payload of <see cref="StoreJobKind.FirefoxSync"/>.
/// </summary>
/// <param name="ConnectionId">Firefox Add-ons connection.</param>
/// <param name="StoreAppId">One add-on only; null for every add-on of the connection (and new add-ons of the account).</param>
/// <param name="PendingOnly">Only add-ons with a version awaiting review (the schedule).</param>
public sealed record FirefoxSyncPayload(int ConnectionId, int? StoreAppId = null, bool PendingOnly = false);

/// <summary>
/// Result of adding an add-on.
/// </summary>
public sealed class FirefoxAddonAddedDto
{
    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>The new store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>The sync job that reads the add-on.</summary>
    public long JobId { get; set; }
}

/// <summary>
/// A text in one locale.
/// </summary>
public sealed class FirefoxLocalizedText
{
    /// <summary>Locale (<c>en-US</c>, <c>de</c>).</summary>
    public string Locale { get; set; }

    /// <summary>Text; null or empty removes the locale's text.</summary>
    public string Value { get; set; }
}

/// <summary>
/// One listing field in one locale.
/// </summary>
public sealed class FirefoxListingValue
{
    /// <summary>Field (<see cref="Utils.Firefox.FirefoxFields"/>).</summary>
    public string Field { get; set; }

    /// <summary>Locale.</summary>
    public string Locale { get; set; }

    /// <summary>Value; null or empty removes the locale's value.</summary>
    public string Value { get; set; }
}

/// <summary>
/// A preview (screenshot) of an add-on.
/// </summary>
public sealed class FirefoxPreviewDto
{
    /// <summary>Preview id.</summary>
    public long Id { get; set; }

    /// <summary>Image.</summary>
    public string ImageUrl { get; set; }

    /// <summary>Thumbnail.</summary>
    public string ThumbnailUrl { get; set; }

    /// <summary>Position.</summary>
    public int Position { get; set; }

    /// <summary>Caption.</summary>
    public string Caption { get; set; }

    /// <summary>Width.</summary>
    public int? Width { get; set; }

    /// <summary>Height.</summary>
    public int? Height { get; set; }
}

/// <summary>
/// A version of an add-on.
/// </summary>
public sealed class FirefoxVersionDto
{
    /// <summary>Version id.</summary>
    public long Id { get; set; }

    /// <summary>Version.</summary>
    public string Version { get; set; }

    /// <summary>Channel.</summary>
    public FirefoxVersionChannel Channel { get; set; }

    /// <summary>File review status.</summary>
    public FirefoxFileStatus FileStatus { get; set; }

    /// <summary>Disabled by the developer.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Release notes per locale.</summary>
    public List<FirefoxLocalizedText> ReleaseNotes { get; set; } = [];

    /// <summary>License.</summary>
    public string License { get; set; }

    /// <summary>Whether source code was uploaded.</summary>
    public bool HasSource { get; set; }

    /// <summary>Created.</summary>
    public DateTime? Created { get; set; }

    /// <summary>Signed file.</summary>
    public string FileUrl { get; set; }

    /// <summary>Data collection permissions declared by the file.</summary>
    public List<string> DataCollectionPermissions { get; set; } = [];
}

/// <summary>
/// A Firefox add-on as the last sync and a live read saw it, with the Firefox packages intake received for the app.
/// </summary>
public sealed class FirefoxAddonDto
{
    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>Connection.</summary>
    public int? ConnectionId { get; set; }

    /// <summary>Numeric AMO id.</summary>
    public long AddonId { get; set; }

    /// <summary>Guid.</summary>
    public string Guid { get; set; }

    /// <summary>Slug.</summary>
    public string Slug { get; set; }

    /// <summary>Status.</summary>
    public FirefoxAddonStatus Status { get; set; }

    /// <summary>Disabled by the developer.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Default locale.</summary>
    public string DefaultLocale { get; set; }

    /// <summary>Listed version on the add-on page.</summary>
    public string ListedVersion { get; set; }

    /// <summary>Newest version of any channel.</summary>
    public string LatestVersion { get; set; }

    /// <summary>Its channel.</summary>
    public FirefoxVersionChannel LatestChannel { get; set; }

    /// <summary>Its file status.</summary>
    public FirefoxFileStatus LatestFileStatus { get; set; }

    /// <summary>Average rating.</summary>
    public double? Rating { get; set; }

    /// <summary>Number of ratings.</summary>
    public long? RatingCount { get; set; }

    /// <summary>Average daily users.</summary>
    public long? AverageDailyUsers { get; set; }

    /// <summary>Weekly downloads.</summary>
    public long? WeeklyDownloads { get; set; }

    /// <summary>Listing values per field and locale (live read; empty when it failed).</summary>
    public List<FirefoxListingValue> Listing { get; set; } = [];

    /// <summary>Category slugs.</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Previews.</summary>
    public List<FirefoxPreviewDto> Previews { get; set; } = [];

    /// <summary>Icon.</summary>
    public string IconUrl { get; set; }

    /// <summary>Versions, newest first (live read).</summary>
    public List<FirefoxVersionDto> Versions { get; set; } = [];

    /// <summary>Why the live read failed, when it did (the synced values are shown).</summary>
    public string ReadError { get; set; }

    /// <summary>Every state change, newest first.</summary>
    public List<FirefoxStateChange> StatusHistory { get; set; } = [];

    /// <summary>Last successful sync.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>Why the last sync failed.</summary>
    public string SyncError { get; set; }

    /// <summary>Public page.</summary>
    public string StoreUrl { get; set; }

    /// <summary>The add-on in the Developer Hub.</summary>
    public string DeveloperHubUrl { get; set; }

    /// <summary>Firefox packages intake received for the app, newest first.</summary>
    public List<FirefoxPackageDto> Packages { get; set; } = [];
}

/// <summary>
/// A Firefox package intake received.
/// </summary>
public sealed class FirefoxPackageDto
{
    /// <summary>Intake session.</summary>
    public long SessionId { get; set; }

    /// <summary>File name.</summary>
    public string FileName { get; set; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>manifest.json version.</summary>
    public string Version { get; set; }

    /// <summary>Add-on id of the manifest.</summary>
    public string GeckoId { get; set; }

    /// <summary>manifest.json name.</summary>
    public string Name { get; set; }

    /// <summary>manifest_version.</summary>
    public int? ManifestVersion { get; set; }

    /// <summary>Intake state.</summary>
    public ArtifactSessionStatus Status { get; set; }

    /// <summary>When it was received.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Whether the file was removed from storage.</summary>
    public bool PackageDeleted { get; set; }

    /// <summary>What the manifest checks found.</summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>Whether one of them blocks a submission.</summary>
    public bool Blocked { get; set; }
}

/// <summary>
/// A file a Firefox plan sends (an asset or an intake package), checked again before it is sent.
/// </summary>
public sealed class FirefoxPlannedFile
{
    /// <summary>Asset id (images, source code); null for an intake package.</summary>
    public long? AssetId { get; set; }

    /// <summary>File name.</summary>
    public string FileName { get; set; }

    /// <summary>Storage key.</summary>
    public string ObjectKey { get; set; }

    /// <summary>Size.</summary>
    public long Size { get; set; }

    /// <summary>SHA-256.</summary>
    public string Sha256 { get; set; }

    /// <summary>Media type.</summary>
    public string ContentType { get; set; }
}

/// <summary>
/// One text a Firefox plan writes.
/// </summary>
public sealed class FirefoxPlannedText
{
    /// <summary>Field (listing) or <c>releaseNotes</c>.</summary>
    public string Field { get; set; }

    /// <summary>Locale.</summary>
    public string Locale { get; set; }

    /// <summary>New text; null removes the locale's text.</summary>
    public string Value { get; set; }
}

/// <summary>
/// The change of a Firefox plan (<see cref="ChangePlanKind.FirefoxChange"/>), stored in the plan: exactly what the approved
/// write sends.
/// </summary>
public sealed class FirefoxPlannedChange
{
    /// <summary>What the plan writes.</summary>
    public FirefoxChangeKind Kind { get; set; }

    /// <summary>Numeric AMO id.</summary>
    public string AddonId { get; set; }

    /// <summary>Listing texts and links; release notes of a version.</summary>
    public List<FirefoxPlannedText> Texts { get; set; } = [];

    /// <summary>Listing: the new category slugs; null keeps them.</summary>
    public List<string> Categories { get; set; }

    /// <summary>Version: intake session of the package.</summary>
    public long? SessionId { get; set; }

    /// <summary>Version: the package.</summary>
    public FirefoxPlannedFile Package { get; set; }

    /// <summary>Version: its version string.</summary>
    public string PackageVersion { get; set; }

    /// <summary>Version: channel.</summary>
    public FirefoxVersionChannel Channel { get; set; }

    /// <summary>Version: notes to the reviewer.</summary>
    public string ApprovalNotes { get; set; }

    /// <summary>Version: license slug; null keeps the previous version's.</summary>
    public string License { get; set; }

    /// <summary>Version: applications (<c>firefox</c>, <c>android</c>); empty uses the manifest.</summary>
    public List<string> Compatibility { get; set; } = [];

    /// <summary>Version: source code archive.</summary>
    public FirefoxPlannedFile Source { get; set; }

    /// <summary>Previews to add, in order.</summary>
    public List<FirefoxPlannedFile> AddPreviews { get; set; } = [];

    /// <summary>Preview ids to remove.</summary>
    public List<long> DeletePreviews { get; set; } = [];

    /// <summary>Icon: the new icon.</summary>
    public FirefoxPlannedFile Icon { get; set; }

    /// <summary>Release notes: the version id.</summary>
    public long? VersionId { get; set; }
}
