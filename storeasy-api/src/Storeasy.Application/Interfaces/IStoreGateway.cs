namespace Storeasy.Application.Interfaces;

/// <summary>
/// Entry point of the store engine for credential handling. Implementations talk to App Store Connect and Google Play;
/// tests use a fake. The secret is the JSON the user pasted: for Apple <c>{"issuerId","keyId","privateKey"}</c>, for
/// Google the service account key file as downloaded.
/// </summary>
public interface IStoreGateway
{
    /// <summary>
    /// Parses and checks the secret offline (shape, key format) and returns its non secret fingerprint. Returns a
    /// failure with a <see cref="MessageKey"/> when the secret is malformed. Makes no network call.
    /// </summary>
    StoreSecretInspection Inspect(StoreKind store, string secretJson);

    /// <summary>
    /// Proves the credentials work with one cheap read call (Apple: list one app; Google: token exchange, plus an edit
    /// probe when <paramref name="packageName"/> is given).
    /// </summary>
    Task<StoreVerification> VerifyAsync(StoreKind store, string secretJson, string packageName, CancellationToken cancellationToken);
}

/// <summary>
/// Result of <see cref="IStoreGateway.Inspect"/>.
/// </summary>
/// <param name="IsValid">Whether the secret is well formed.</param>
/// <param name="ErrorKey">Message key of the problem when invalid.</param>
/// <param name="Fingerprint">Non secret identifiers when valid.</param>
/// <param name="NormalizedSecret">Canonical JSON to store (whitespace and unknown fields removed for Apple).</param>
public sealed record StoreSecretInspection(bool IsValid, string ErrorKey, ConnectionFingerprint Fingerprint, string NormalizedSecret);

/// <summary>
/// Result of <see cref="IStoreGateway.VerifyAsync"/>.
/// </summary>
/// <param name="Status">Verdict.</param>
/// <param name="Error">Redacted, short reason when not verified.</param>
public sealed record StoreVerification(ConnectionStatus Status, string Error);
