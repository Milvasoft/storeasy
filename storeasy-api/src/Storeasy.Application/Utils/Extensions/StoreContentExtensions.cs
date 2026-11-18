using Milvasoft.Components.Rest.MilvaResponse;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// A store record and its live content, or the message key (and redacted store detail) of why it could not be read.
/// </summary>
/// <param name="App">Store record, null when not found.</param>
/// <param name="Snapshot">Store content, null on failure.</param>
/// <param name="ErrorKey">Message key of the failure.</param>
/// <param name="ErrorDetail">Redacted store message.</param>
public sealed record StoreContentResult(StoreAppContext App, StoreContentSnapshot Snapshot, string ErrorKey, string ErrorDetail)
{
    /// <summary>
    /// Whether the record was found and read.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;

    /// <summary>
    /// The failure as a response.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Response<T> Error<T>() => this.ErrorDetail == null
        ? Response<T>.Error(default, this.ErrorKey)
        : Response<T>.Error(default, new ResponseMessage(this.ErrorKey, this.ErrorDetail));
}

/// <summary>
/// Shared steps of the store content handlers.
/// </summary>
public static class StoreContentExtensions
{
    /// <summary>
    /// Resolves store record <paramref name="storeAppId"/> in the organization and reads <paramref name="parts"/> of it
    /// live. A record of another organization is not found. <paramref name="store"/> restricts the record to one store
    /// (<see cref="MessageKey.StoreOperationUnsupported"/> otherwise).
    /// </summary>
    /// <param name="contentService"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="parts"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="versionId"></param>
    /// <param name="store"></param>
    /// <returns></returns>
    public static async Task<StoreContentResult> ReadStoreAppAsync(this IStoreContentService contentService,
                                                                   int organizationId,
                                                                   int storeAppId,
                                                                   ContentParts parts,
                                                                   CancellationToken cancellationToken,
                                                                   string versionId = null,
                                                                   StoreKind? store = null)
    {
        var app = await contentService.GetStoreAppAsync(organizationId, storeAppId, cancellationToken);

        return await contentService.ReadContentAsync(app, parts, versionId, store, cancellationToken);
    }

    /// <summary>
    /// Reads <paramref name="parts"/> of an already resolved record (null: not found).
    /// </summary>
    /// <param name="contentService"></param>
    /// <param name="app"></param>
    /// <param name="parts"></param>
    /// <param name="versionId"></param>
    /// <param name="store"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<StoreContentResult> ReadContentAsync(this IStoreContentService contentService, StoreAppContext app, ContentParts parts, string versionId, StoreKind? store, CancellationToken cancellationToken)
    {
        if (app == null)
            return new StoreContentResult(null, null, MessageKey.StoreAppNotFound, null);

        if (store != null && app.Store != store)
            return new StoreContentResult(app, null, MessageKey.StoreOperationUnsupported, null);

        var read = await contentService.ReadAsync(app, parts, versionId, cancellationToken);

        return new StoreContentResult(app, read.Snapshot, read.ErrorKey, read.ErrorDetail);
    }
}
