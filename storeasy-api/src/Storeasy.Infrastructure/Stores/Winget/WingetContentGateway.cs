using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// The content read of a winget package (<see cref="WingetContent"/>): the version list and the manifests of one version
/// from winget-pkgs. Writes do not go through here: a winget plan is applied by <see cref="WingetPlanExtension"/>, which
/// opens a pull request.
/// </summary>
/// <param name="client"></param>
public sealed class WingetContentGateway(IWingetClient client) : IStoreContentGateway
{
    private readonly IWingetClient _client = client;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Winget;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
    {
        var versions = await _client.ListVersionsAsync(target.SecretJson, target.ExternalId, cancellationToken);
        var version = target.VersionId == null ? versions.FirstOrDefault() : versions.FirstOrDefault(v => string.Equals(v, target.VersionId, StringComparison.OrdinalIgnoreCase));

        if (target.VersionId != null && version == null)
            throw new StoreCallException($"404: Version {target.VersionId} of {target.ExternalId} is not in winget-pkgs.", 404, false);

        var files = version == null ? new Dictionary<string, string>() : await _client.ReadManifestFilesAsync(target.SecretJson, target.ExternalId, version, cancellationToken);

        return WingetContent.ToSnapshot(target.ExternalId, versions, version, files);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoreOption>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
        => throw new StoreJobException("winget changes are winget pull request plans; plan them with winget_change_plan.", transient: false);
}
