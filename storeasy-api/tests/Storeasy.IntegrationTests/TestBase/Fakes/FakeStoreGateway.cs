using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Replaces the store engine's network side. <see cref="Inspect"/> runs the real offline parser (so malformed secrets
/// are still rejected); <see cref="VerifyAsync"/> records the opened secret and answers <see cref="NextVerification"/>
/// instead of calling App Store Connect or Google Play.
/// </summary>
public sealed class FakeStoreGateway : IStoreGateway
{
    private readonly StoreGateway _offline = new(null, TimeProvider.System, null);

    /// <summary>
    /// What the next verification answers.
    /// </summary>
    public StoreVerification NextVerification { get; set; } = new(ConnectionStatus.Verified, null);

    /// <summary>
    /// Secrets the verifications received, in order (proves the stored secret opens to the original).
    /// </summary>
    public ConcurrentQueue<string> VerifiedSecrets { get; } = new();

    /// <inheritdoc/>
    private IReadOnlyList<IStoreChannel> _channels = [];

    /// <summary>
    /// Hands the calls of the registered channels (winget) to their real engines; returns this gateway.
    /// </summary>
    public FakeStoreGateway WithChannels(IEnumerable<IStoreChannel> channels)
    {
        _channels = [.. channels];

        return this;
    }

    private IStoreChannel ChannelOf(StoreKind store) => _channels.LastOrDefault(c => c.Store == store);

    public StoreSecretInspection Inspect(StoreKind store, string secretJson) => ChannelOf(store)?.Inspect(secretJson) ?? _offline.Inspect(store, secretJson);

    /// <summary>
    /// App Store Connect apps by the key id of the API key that sees them (each test uses its own key id).
    /// </summary>
    public ConcurrentDictionary<string, List<StoreAppRecord>> AppleApps { get; } = new();

    /// <summary>
    /// Play apps the service accounts were invited to, by package name.
    /// </summary>
    public ConcurrentDictionary<string, StoreAppRecord> GoogleApps { get; } = new();

    /// <summary>
    /// Builds by app external id.
    /// </summary>
    public ConcurrentDictionary<string, List<StoreBuildRecord>> Builds { get; } = new();

    /// <summary>
    /// Store calls that should fail, by app external id, with the exception to throw.
    /// </summary>
    public ConcurrentDictionary<string, StoreCallException> Failures { get; } = new();

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(StoreKind store, string secretJson, CancellationToken cancellationToken)
    {
        if (ChannelOf(store) is { } channel)
            return channel.ListAppsAsync(secretJson, cancellationToken);

        if (store != StoreKind.Apple)
            throw new StoreCallException($"{store} cannot list apps.", null, false);

        var keyId = JsonDocument.Parse(secretJson).RootElement.GetProperty("keyId").GetString();

        return Task.FromResult<IReadOnlyList<StoreAppRecord>>([.. AppleApps.GetValueOrDefault(keyId) ?? []]);
    }

    /// <inheritdoc/>
    public Task<StoreAppRecord> GetAppAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        if (ChannelOf(store) is { } channel)
            return channel.GetAppAsync(secretJson, externalId, cancellationToken);

        if (Failures.TryGetValue(externalId, out var failure))
            throw failure;

        var app = (store == StoreKind.Google ? GoogleApps.GetValueOrDefault(externalId) : AppleApps.Values.SelectMany(a => a).FirstOrDefault(a => a.ExternalId == externalId))
                  ?? throw new StoreCallException("404: The caller does not have permission or the app does not exist.", 404, false);

        return Task.FromResult(app);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        if (ChannelOf(store) is { } channel)
            return channel.ListBuildsAsync(secretJson, externalId, cancellationToken);

        if (Failures.TryGetValue(externalId, out var failure))
            throw failure;

        return Task.FromResult<IReadOnlyList<StoreBuildRecord>>([.. Builds.GetValueOrDefault(externalId) ?? []]);
    }

    /// <summary>
    /// Icon addresses by app external id; an app without an entry has no icon in the store.
    /// </summary>
    public ConcurrentDictionary<string, string> Icons { get; } = new();

    /// <summary>
    /// Icon reads that should fail, by app external id, with the exception to throw (the other reads still work).
    /// </summary>
    public ConcurrentDictionary<string, StoreCallException> IconFailures { get; } = new();

    /// <inheritdoc/>
    public Task<string> GetIconUrlAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken)
    {
        if (ChannelOf(store) is { } channel)
            return channel.GetIconUrlAsync(secretJson, externalId, cancellationToken);

        if (IconFailures.TryGetValue(externalId, out var failure))
            throw failure;

        return Task.FromResult(Icons.GetValueOrDefault(externalId));
    }

    /// <inheritdoc/>
    public Task<StoreVerification> VerifyAsync(StoreKind store, string secretJson, string packageName, CancellationToken cancellationToken)
    {
        if (ChannelOf(store) is { } channel)
            return channel.VerifyAsync(secretJson, packageName, cancellationToken);

        VerifiedSecrets.Enqueue(secretJson);

        return Task.FromResult(NextVerification);
    }
}
