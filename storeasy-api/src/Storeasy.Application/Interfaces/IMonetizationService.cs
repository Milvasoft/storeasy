using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Subscriptions and In-App Purchases of the request pipeline: live reads through the store's
/// <see cref="IMonetizationGateway"/> (the connection secret is opened for one call), planning (read, diff, fingerprint,
/// save through <see cref="IChangePlanService"/>) and the sync that keeps the last read products in storeasy. Store
/// failures come back as a message key, never as an exception.
/// </summary>
public interface IMonetizationService
{
    /// <summary>
    /// Reads the record's products live.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="parts"></param>
    /// <param name="productIds">Products whose details are read; null for every product.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<MonetizationRead> ReadAsync(StoreAppContext app, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// Price points of a product in one territory.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="kind"></param>
    /// <param name="productExternalId"></param>
    /// <param name="territory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<List<PricePointDto>>> ListPricePointsAsync(StoreAppContext app, StoreProductKind kind, string productExternalId, string territory, CancellationToken cancellationToken);

    /// <summary>
    /// Play's conversion of a price to every region.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="currency"></param>
    /// <param name="amount"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ConvertedPricesDto>> ConvertPricesAsync(StoreAppContext app, string currency, string amount, CancellationToken cancellationToken);

    /// <summary>
    /// Sandbox testers of the record's developer account.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<List<SandboxTesterDto>>> ListSandboxTestersAsync(StoreAppContext app, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a plan of <paramref name="changes"/>: reads the touched products live, asks the store's equalization for
    /// base prices, checks review screenshot uploads, diffs and saves the plan (approved right away when
    /// <paramref name="dryRun"/> is false, with the same checks as an approval).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="kind"></param>
    /// <param name="changes"></param>
    /// <param name="dryRun"></param>
    /// <param name="confirm"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ChangePlanDto>> PlanAsync(StoreAppContext app, ChangePlanKind kind, List<MonetizationChangeDto> changes, bool dryRun, string confirm, CancellationToken cancellationToken);

    /// <summary>
    /// Approves a pending plan of one of <paramref name="kinds"/> (any other plan is not found here).
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="planId"></param>
    /// <param name="confirm"></param>
    /// <param name="kinds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response<ChangePlanDto>> ApproveAsync(int organizationId, long planId, string confirm, IReadOnlyCollection<ChangePlanKind> kinds, CancellationToken cancellationToken);

    /// <summary>
    /// Saves a full read of the record's products (upserts by the store's id, deletes what the store no longer has).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="snapshot"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Number of products saved.</returns>
    Task<int> SaveAsync(StoreAppContext app, MonetizationSnapshot snapshot, CancellationToken cancellationToken);

    /// <summary>
    /// Queues a <see cref="StoreJobKind.SyncMonetization"/> job of the record.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The job id.</returns>
    Task<long> QueueSyncAsync(StoreAppContext app, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a live monetization read.
/// </summary>
/// <param name="Snapshot">Products.</param>
/// <param name="ErrorKey">Message key of the failure.</param>
/// <param name="ErrorDetail">Redacted store message.</param>
public sealed record MonetizationRead(MonetizationSnapshot Snapshot, string ErrorKey = null, string ErrorDetail = null)
{
    /// <summary>
    /// Whether the read succeeded.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;

    /// <summary>
    /// The failure as a response.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public Response<T> Error<T>() => new StoreContentResult(null, null, this.ErrorKey, this.ErrorDetail).Error<T>();
}
