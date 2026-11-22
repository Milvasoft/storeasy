using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// The content read of a Firefox add-on (<see cref="FirefoxContent"/>): the add-on detail with every translation and its
/// listed and unlisted versions. Writes do not go through here: a Firefox plan is applied by
/// <see cref="FirefoxPlanExtension"/>.
/// </summary>
/// <param name="client"></param>
/// <param name="options"></param>
public sealed class FirefoxContentGateway(IAmoClient client, FirefoxOptions options) : IStoreContentGateway
{
    private readonly IAmoClient _client = client;
    private readonly FirefoxOptions _options = options;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Firefox;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
        => FirefoxContent.ToSnapshot(await ReadStateAsync(_client, target.SecretJson, target.ExternalId, _options.VersionPages, cancellationToken));

    /// <summary>
    /// Reads the add-on and its versions.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="versionPages"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<FirefoxAddonState> ReadStateAsync(IAmoClient client, string secretJson, string addon, int versionPages, CancellationToken cancellationToken)
    {
        var detail = await client.GetAddonAsync(secretJson, addon, cancellationToken);
        var versions = await client.ListVersionsAsync(secretJson, detail.Id > 0 ? detail.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : addon, versionPages, cancellationToken);

        return new FirefoxAddonState(detail, versions);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreOption>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
        => throw new StoreJobException("Firefox Add-ons changes are Firefox plans; plan them with firefox_change_plan.", transient: false);
}
