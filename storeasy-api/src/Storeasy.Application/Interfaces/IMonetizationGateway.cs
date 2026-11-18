using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// The monetization engine of one store: reads an app's subscriptions and In-App Purchases (App Store Connect) or
/// subscriptions and one-time products (Google Play), lists price points, asks the store's own equalization or price
/// conversion, and writes a <see cref="MonetizationChangeSet"/>. Writes run only inside the apply job. Store failures
/// surface as <see cref="StoreCallException"/>; 429 and 5xx answers are retried with backoff by the transport.
/// </summary>
public interface IMonetizationGateway
{
    /// <summary>
    /// Store this gateway talks to.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Reads the app's products.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="parts">What to read beyond the product list.</param>
    /// <param name="productIds">Products whose details are read; null for every product.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<MonetizationSnapshot> ReadAsync(StoreContentTarget target, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken);

    /// <summary>
    /// Price points of a product in one territory (App Store).
    /// </summary>
    /// <param name="target"></param>
    /// <param name="kind"></param>
    /// <param name="productExternalId"></param>
    /// <param name="territory"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<PricePointDto>> ListPricePointsAsync(StoreContentTarget target, StoreProductKind kind, string productExternalId, string territory, CancellationToken cancellationToken);

    /// <summary>
    /// The store's equalized price points of every other territory for a base price point (App Store).
    /// </summary>
    /// <param name="target"></param>
    /// <param name="kind"></param>
    /// <param name="pricePointId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<PricePointDto>> EqualizeAsync(StoreContentTarget target, StoreProductKind kind, string pricePointId, CancellationToken cancellationToken);

    /// <summary>
    /// Play's conversion of a price to every region (nothing is saved).
    /// </summary>
    /// <param name="target"></param>
    /// <param name="currency"></param>
    /// <param name="amount"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<ConvertedPricesDto> ConvertPricesAsync(StoreContentTarget target, string currency, string amount, CancellationToken cancellationToken);

    /// <summary>
    /// Sandbox testers of the developer account (App Store).
    /// </summary>
    /// <param name="target"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<SandboxTesterDto>> ListSandboxTestersAsync(StoreContentTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="changes"/>, one step per operation. Every operation reads what it needs again and writes
    /// only what still differs, so a retried job does not repeat finished work.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="changes"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>Short outcome lines.</returns>
    Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, MonetizationChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken);
}
