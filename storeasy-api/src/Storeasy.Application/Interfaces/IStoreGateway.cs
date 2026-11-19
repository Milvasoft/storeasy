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

    /// <summary>
    /// Lists every app the credentials can see. Only stores with a list API (App Store Connect); Google Play has none
    /// (apps are added by package name) and Microsoft Store is not supported yet: both throw a permanent
    /// <see cref="StoreCallException"/>.
    /// </summary>
    Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(StoreKind store, string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one app by its store id (App Store Connect app id, Play package name). For Google Play this is also the proof
    /// that the service account was invited to the app (an edit is opened and deleted).
    /// </summary>
    Task<StoreAppRecord> GetAppAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the builds of one app (App Store Connect builds with their versions; Play bundles with the tracks they are
    /// released to), newest first where the store orders them.
    /// </summary>
    Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the address of one app's icon, read only (App Store Connect: the icon asset of the newest build that has one;
    /// Google Play: the <c>icon</c> image of the default language listing, read inside an edit that is deleted afterwards;
    /// Microsoft Store: the largest square logo of the product in the public display catalog, read without credentials, so
    /// <paramref name="secretJson"/> is not used). Returns null when the store has no icon for the app (for Microsoft Store
    /// also when the product is not published), or without a call for a store that has no icons.
    /// </summary>
    Task<string> GetIconUrlAsync(StoreKind store, string secretJson, string externalId, CancellationToken cancellationToken);
}

/// <summary>
/// An app as a store reports it.
/// </summary>
/// <param name="ExternalId">The store's id of the app.</param>
/// <param name="BundleId">Platform identifier (bundle id, package name).</param>
/// <param name="Name">Name in the store.</param>
public sealed record StoreAppRecord(string ExternalId, string BundleId, string Name);

/// <summary>
/// A build as a store reports it.
/// </summary>
/// <param name="ExternalId">The store's id of the build.</param>
/// <param name="Version">User facing version, if known.</param>
/// <param name="BuildNumber">Build number or version code.</param>
/// <param name="ProcessingState">Processing state.</param>
/// <param name="UploadedDate">UTC upload instant, if known.</param>
/// <param name="ExpiryDate">UTC expiry instant (TestFlight), if any.</param>
/// <param name="Tracks">Tracks the build is released to, comma separated (Play).</param>
/// <param name="MinOsVersion">Minimum OS version, if known.</param>
public sealed record StoreBuildRecord(string ExternalId,
                                      string Version,
                                      string BuildNumber,
                                      BuildProcessingState ProcessingState,
                                      DateTime? UploadedDate,
                                      DateTime? ExpiryDate,
                                      string Tracks,
                                      string MinOsVersion);

/// <summary>
/// A store call failed. The message is redacted and safe to store. 429 and 5xx answers and network failures are
/// transient (retried by the job runner); 4xx answers and unsupported operations are permanent.
/// </summary>
/// <param name="message"></param>
/// <param name="statusCode">HTTP status of the store's answer; null for a network failure or an unsupported call.</param>
/// <param name="transient"></param>
public class StoreCallException(string message, int? statusCode, bool transient) : StoreJobException(message, transient)
{
    /// <summary>
    /// HTTP status of the store's answer, if there was one.
    /// </summary>
    public int? StatusCode { get; } = statusCode;
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
