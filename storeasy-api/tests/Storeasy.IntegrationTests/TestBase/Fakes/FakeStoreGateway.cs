using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using System.Collections.Concurrent;

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
    public StoreSecretInspection Inspect(StoreKind store, string secretJson) => _offline.Inspect(store, secretJson);

    /// <inheritdoc/>
    public Task<StoreVerification> VerifyAsync(StoreKind store, string secretJson, string packageName, CancellationToken cancellationToken)
    {
        VerifiedSecrets.Enqueue(secretJson);

        return Task.FromResult(NextVerification);
    }
}
