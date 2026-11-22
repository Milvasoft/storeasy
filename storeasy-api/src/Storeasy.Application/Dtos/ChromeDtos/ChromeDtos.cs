namespace Storeasy.Application.Dtos.ChromeDtos;

/// <summary>
/// Payload of <see cref="StoreJobKind.ChromeSync"/>.
/// </summary>
/// <param name="ConnectionId">Chrome Web Store connection.</param>
/// <param name="StoreAppId">One item only; null for every item of the connection.</param>
/// <param name="PendingOnly">Only items with a submission in review or an upload in progress (the schedule).</param>
public sealed record ChromeSyncPayload(int ConnectionId, int? StoreAppId = null, bool PendingOnly = false);

/// <summary>
/// Result of adding an item.
/// </summary>
public sealed class ChromeItemAddedDto
{
    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// The new store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The sync job that reads the item.
    /// </summary>
    public long JobId { get; set; }
}

/// <summary>
/// A Chrome Web Store item as the last sync read it, with the extension packages intake received for the app.
/// </summary>
public sealed class ChromeItemDto
{
    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>App name.</summary>
    public string AppName { get; set; }

    /// <summary>Extension id.</summary>
    public string ItemId { get; set; }

    /// <summary>Connection.</summary>
    public int? ConnectionId { get; set; }

    /// <summary>Publisher id of the connection.</summary>
    public string PublisherId { get; set; }

    /// <summary>State of the published revision.</summary>
    public ChromeItemState PublishedState { get; set; }

    /// <summary>Version of the published revision.</summary>
    public string PublishedVersion { get; set; }

    /// <summary>Deploy percentage of the published revision.</summary>
    public int? PublishedDeployPercentage { get; set; }

    /// <summary>State of the submitted revision.</summary>
    public ChromeItemState SubmittedState { get; set; }

    /// <summary>Version of the submitted revision.</summary>
    public string SubmittedVersion { get; set; }

    /// <summary>Deploy percentage of the submitted revision.</summary>
    public int? SubmittedDeployPercentage { get; set; }

    /// <summary>State of the last upload.</summary>
    public ChromeUploadState LastUploadState { get; set; }

    /// <summary>Version of the last package storeasy uploaded.</summary>
    public string UploadedVersion { get; set; }

    /// <summary>Warned for a policy violation.</summary>
    public bool Warned { get; set; }

    /// <summary>Taken down for a policy violation.</summary>
    public bool TakenDown { get; set; }

    /// <summary>Average rating.</summary>
    public double? Rating { get; set; }

    /// <summary>Number of ratings.</summary>
    public long? RatingCount { get; set; }

    /// <summary>Seven day active users.</summary>
    public long? WeeklyUsers { get; set; }

    /// <summary>Every state change, newest first.</summary>
    public List<ChromeStateChange> StatusHistory { get; set; } = [];

    /// <summary>Last successful sync.</summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>Why the last sync failed.</summary>
    public string SyncError { get; set; }

    /// <summary>Store page.</summary>
    public string StoreUrl { get; set; }

    /// <summary>The item in the developer dashboard (listing, privacy practices, distribution).</summary>
    public string DashboardUrl { get; set; }

    /// <summary>Extension packages intake received for the app, newest first.</summary>
    public List<ChromePackageDto> Packages { get; set; } = [];
}

/// <summary>
/// An extension package intake received.
/// </summary>
public sealed class ChromePackageDto
{
    /// <summary>Intake session.</summary>
    public long SessionId { get; set; }

    /// <summary>File name.</summary>
    public string FileName { get; set; }

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>manifest.json version.</summary>
    public string Version { get; set; }

    /// <summary>manifest.json name.</summary>
    public string Name { get; set; }

    /// <summary>manifest_version.</summary>
    public int? ManifestVersion { get; set; }

    /// <summary>Intake state.</summary>
    public ArtifactSessionStatus Status { get; set; }

    /// <summary>When it was received.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Whether the file was removed from storage (it cannot be uploaded any more).</summary>
    public bool PackageDeleted { get; set; }

    /// <summary>Whether intake uploaded it to the item already.</summary>
    public bool Uploaded { get; set; }

    /// <summary>What the manifest checks found.</summary>
    public List<string> Issues { get; set; } = [];

    /// <summary>Whether one of them blocks a submission.</summary>
    public bool Blocked { get; set; }
}

/// <summary>
/// The change of a Chrome plan (<see cref="ChangePlanKind.ChromeChange"/>), stored in the plan: exactly what the approved
/// write sends.
/// </summary>
public sealed class ChromePlannedChange
{
    /// <summary>What the plan writes.</summary>
    public ChromeChangeKind Kind { get; set; }

    /// <summary>Extension id.</summary>
    public string ItemId { get; set; }

    /// <summary>Intake session whose package is uploaded first; null to submit what is uploaded.</summary>
    public long? SessionId { get; set; }

    /// <summary>Storage key of that package.</summary>
    public string ObjectKey { get; set; }

    /// <summary>Its size.</summary>
    public long Size { get; set; }

    /// <summary>Its SHA-256, checked again before the upload.</summary>
    public string Sha256 { get; set; }

    /// <summary>Version of the uploaded or submitted package, when known.</summary>
    public string PackageVersion { get; set; }

    /// <summary>Publish: stage after approval instead of publishing.</summary>
    public bool Staged { get; set; }

    /// <summary>Publish: initial deploy percentage; deploy percentage: the new one.</summary>
    public int? DeployPercentage { get; set; }

    /// <summary>Publish: ask the store to skip the review.</summary>
    public bool SkipReview { get; set; }

    /// <summary>Rollback: earlier version to publish again; null for the last one.</summary>
    public string TargetVersion { get; set; }

    /// <summary>Rollback: version number given to it.</summary>
    public string NewVersion { get; set; }

    /// <summary>Rollback: reason kept by the store.</summary>
    public string Reason { get; set; }
}
