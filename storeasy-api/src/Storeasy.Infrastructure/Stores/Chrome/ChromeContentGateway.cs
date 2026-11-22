using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// The content read of a Chrome Web Store item (<see cref="ChromeContent"/>): its status. The API has no read or write of
/// the store listing, so listing plans are not possible; writes are Chrome plans applied by <see cref="ChromePlanExtension"/>.
/// </summary>
/// <param name="client"></param>
public sealed class ChromeContentGateway(IChromeWebStoreClient client) : IStoreContentGateway
{
    private readonly IChromeWebStoreClient _client = client;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Chrome;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
        => ChromeContent.ToSnapshot(target.ExternalId, await _client.FetchStatusAsync(target.SecretJson, target.ExternalId, cancellationToken));

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreOption>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
        => throw new StoreJobException("Chrome Web Store changes are Chrome plans (chrome_change_plan); the store listing has no API and is edited in the developer dashboard.", transient: false);
}
