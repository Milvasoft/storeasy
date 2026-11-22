using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// The winget engine as a store channel: the credential is a GitHub token with the owner's fork of winget-pkgs, a product
/// is a package identifier, its builds are the version folders in winget-pkgs. winget has no API that lists a publisher's
/// packages, so packages are added by identifier.
/// </summary>
/// <param name="client"></param>
public sealed class WingetChannel(IWingetClient client) : IStoreChannel
{
    private readonly IWingetClient _client = client;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Winget;

    /// <inheritdoc/>
    public StoreSecretInspection Inspect(string secretJson) => InspectSecret(secretJson);

    /// <summary>
    /// The offline check of a winget secret (also used without a client, by the tests' gateway).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    public static StoreSecretInspection InspectSecret(string secretJson)
    {
        var credentials = WingetCredentialParser.Parse(secretJson, out var errorKey);

        if (credentials == null)
            return new StoreSecretInspection(false, errorKey, null, null);

        var fingerprint = new ConnectionFingerprint
        {
            AccountLogin = credentials.ForkOwner,
            Repository = credentials.Fork,
            TokenKind = WingetCredentialParser.KindOf(credentials.Token),
        };

        return new StoreSecretInspection(true, null, fingerprint, JsonSerializer.Serialize(credentials));
    }

    /// <inheritdoc/>
    public Task<StoreVerification> VerifyAsync(string secretJson, string productId, CancellationToken cancellationToken) => _client.VerifyAsync(secretJson, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(string secretJson, CancellationToken cancellationToken)
        => throw new StoreCallException("winget has no API that lists a publisher's packages; add the package by its identifier.", null, false);

    /// <inheritdoc/>
    public async Task<StoreAppRecord> GetAppAsync(string secretJson, string externalId, CancellationToken cancellationToken)
    {
        var versions = await _client.ListVersionsAsync(secretJson, externalId, cancellationToken);

        if (versions.Count == 0)
            return new StoreAppRecord(externalId, externalId, externalId);

        var set = WingetManifestSet.FromFiles(await _client.ReadManifestFilesAsync(secretJson, externalId, versions[0], cancellationToken));

        return new StoreAppRecord(externalId, externalId, set.DefaultLocale.Text("PackageName") ?? externalId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(string secretJson, string externalId, CancellationToken cancellationToken)
        => [.. (await _client.ListVersionsAsync(secretJson, externalId, cancellationToken)).Select(v => new StoreBuildRecord(v, v, v, BuildProcessingState.Valid, null, null, null, null))];

    /// <inheritdoc/>
    public async Task<string> GetIconUrlAsync(string secretJson, string externalId, CancellationToken cancellationToken)
    {
        var versions = await _client.ListVersionsAsync(secretJson, externalId, cancellationToken);

        if (versions.Count == 0)
            return null;

        var set = WingetManifestSet.FromFiles(await _client.ReadManifestFilesAsync(secretJson, externalId, versions[0], cancellationToken));

        return IconOf(set);
    }

    /// <summary>
    /// The first https <c>IconUrl</c> of the default locale's <c>Icons</c> (winget-pkgs manifests rarely carry one).
    /// </summary>
    /// <param name="set"></param>
    /// <returns></returns>
    public static string IconOf(WingetManifestSet set)
        => (set.DefaultLocale.Seq("Icons") ?? []).OfType<YamlMap>()
                                                  .Select(i => i.Text("IconUrl"))
                                                  .FirstOrDefault(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);

    /// <inheritdoc/>
    public StoreJobRequest SyncJob(int organizationId, int connectionId)
        => new(organizationId, StoreJobKind.WingetSync, new WingetSyncPayload(connectionId), StoreKind.Winget);

    /// <inheritdoc/>
    public bool SameAccount(ConnectionFingerprint current, ConnectionFingerprint next)
        => current?.Repository == null || string.Equals(current.Repository, next?.Repository, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public string CredentialLabel(ConnectionFingerprint fingerprint)
        => fingerprint?.Repository == null ? null : $"{fingerprint.TokenKind ?? "GitHub"} token for {fingerprint.Repository}";
}
