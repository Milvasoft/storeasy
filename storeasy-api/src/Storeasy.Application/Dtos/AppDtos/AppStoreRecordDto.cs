namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// An app's record in one store.
/// </summary>
public class AppStoreRecordDto
{
    /// <summary>
    /// Store record id.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the app (App Store Connect app id, Play package name, Microsoft Store product id).
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Platform identifier (bundle id, package name).
    /// </summary>
    public string BundleId { get; set; }

    /// <summary>
    /// Name in the store.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Connection the record is read with.
    /// </summary>
    public int? StoreConnectionId { get; set; }

    /// <summary>
    /// UTC instant of the last successful sync; null until the first sync confirmed the record.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// State of the record's latest known store version as the store names it (<c>WAITING_FOR_REVIEW</c>, <c>IN_REVIEW</c>,
    /// <c>READY_FOR_SALE</c>, a Microsoft Store submission status); only on the app detail, null when storeasy has not
    /// seen a version of the record (Google Play has no review state in its API).
    /// </summary>
    public string VersionState { get; set; }

    /// <summary>
    /// User facing version of <see cref="VersionState"/> (1.4).
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Build number attached to that version, if known.
    /// </summary>
    public string VersionBuildNumber { get; set; }

    /// <summary>
    /// UTC instant <see cref="VersionState"/> was last changed.
    /// </summary>
    public DateTime? VersionStateDate { get; set; }
}
