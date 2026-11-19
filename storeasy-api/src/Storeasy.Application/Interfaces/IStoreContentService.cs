using Storeasy.Domain.Rules;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Live reads of a store app's content for the request pipeline (listing screens, planning, readiness). Resolves the store
/// record inside the organization, opens its connection secret for the duration of the call only and asks the store's
/// <see cref="IStoreContentGateway"/>. Store failures come back as a message key, never as an exception.
/// </summary>
public interface IStoreContentService
{
    /// <summary>
    /// The store record <paramref name="storeAppId"/> of the organization, or null (also for a record of another organization).
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreAppContext> GetStoreAppAsync(int organizationId, int storeAppId, CancellationToken cancellationToken);

    /// <summary>
    /// The store record of <paramref name="store"/> of app <paramref name="appId"/>, or null.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="store"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreAppContext> GetStoreAppOfAppAsync(int organizationId, int appId, StoreKind store, CancellationToken cancellationToken);

    /// <summary>
    /// Reads <paramref name="parts"/> of the record's content from the store.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="parts"></param>
    /// <param name="versionId">App Store version; null for the editable one.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreContentRead> ReadAsync(StoreAppContext app, ContentParts parts, string versionId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists store choices (categories, price points, territories).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="kind"></param>
    /// <param name="filter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreOptionsRead> ListOptionsAsync(StoreAppContext app, StoreOptionKind kind, string filter, CancellationToken cancellationToken);

    /// <summary>
    /// Every store record of the organization's shown apps (hidden apps are left out of these organization wide reads),
    /// or of one app when <paramref name="appId"/> is set, hidden or not.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<StoreAppContext>> GetStoreAppsAsync(int organizationId, int? appId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads uploaded assets of the organization from storage and checks them: checksums, image size and alpha. Assets
    /// that do not exist in the organization or are not uploaded are left out.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="assetIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<InspectedAsset>> InspectAssetsAsync(int organizationId, IReadOnlyCollection<long> assetIds, CancellationToken cancellationToken);

    /// <summary>
    /// The catalog subject of the record: the app profile completed with what the store and the synced builds tell.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="snapshot">Store content, when read; null otherwise.</param>
    /// <returns></returns>
    RuleSubject Subject(StoreAppContext app, StoreContentSnapshot snapshot);
}

/// <summary>
/// A store record resolved inside an organization.
/// </summary>
/// <param name="OrganizationId">Organization.</param>
/// <param name="StoreAppId">Store record.</param>
/// <param name="AppId">storeasy app.</param>
/// <param name="AppName">storeasy app name.</param>
/// <param name="Store">Store.</param>
/// <param name="ExternalId">The store's id of the app.</param>
/// <param name="ConnectionId">Connection the record is read with; null when it was removed.</param>
/// <param name="Profile">Catalog profile of the app.</param>
public sealed record StoreAppContext(int OrganizationId, int StoreAppId, int AppId, string AppName, StoreKind Store, string ExternalId, int? ConnectionId, AppProfile Profile);

/// <summary>
/// Result of a live read: the snapshot, or a message key (and a redacted store message) when the read failed.
/// </summary>
/// <param name="Snapshot">Store content.</param>
/// <param name="ErrorKey">Message key of the failure.</param>
/// <param name="ErrorDetail">Redacted store message.</param>
public sealed record StoreContentRead(StoreContentSnapshot Snapshot, string ErrorKey = null, string ErrorDetail = null)
{
    /// <summary>
    /// Whether the read succeeded.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;
}

/// <summary>
/// Result of listing store choices.
/// </summary>
/// <param name="Options">Choices.</param>
/// <param name="ErrorKey">Message key of the failure.</param>
/// <param name="ErrorDetail">Redacted store message.</param>
public sealed record StoreOptionsRead(IReadOnlyList<StoreOption> Options, string ErrorKey = null, string ErrorDetail = null);

/// <summary>
/// An uploaded asset as read back from storage.
/// </summary>
/// <param name="AssetId">Asset id.</param>
/// <param name="ObjectKey">Object key.</param>
/// <param name="FileName">File name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="Md5">Lower case hex MD5.</param>
/// <param name="Sha256">Lower case hex SHA-256.</param>
/// <param name="Image">Image header; null for other files.</param>
public sealed record InspectedAsset(long AssetId, string ObjectKey, string FileName, string ContentType, long Size, string Md5, string Sha256, Domain.Assets.ImageHeader Image);
