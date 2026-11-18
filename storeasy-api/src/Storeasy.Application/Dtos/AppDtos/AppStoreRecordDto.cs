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
}
