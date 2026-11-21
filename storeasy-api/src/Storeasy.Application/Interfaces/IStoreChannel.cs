namespace Storeasy.Application.Interfaces;

/// <summary>
/// The engine of one store or distribution channel that plugs into the shared store code without a branch for it: the
/// store gateway (<see cref="IStoreGateway"/>) hands its calls for <see cref="Store"/> to the channel, "sync apps" queues
/// the channel's own sync job, and replacing a connection's key asks the channel whether the new credential is for the same
/// account. Registered as a singleton; one per <see cref="StoreKind"/>. The first three stores predate this interface and
/// are wired in directly; every channel after them implements it (docs/adding-a-store.md).
/// </summary>
public interface IStoreChannel
{
    /// <summary>
    /// The store this engine serves.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Parses and checks a credential offline: its fingerprint (without the secret) and the normalized JSON to encrypt.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    StoreSecretInspection Inspect(string secretJson);

    /// <summary>
    /// Checks a credential against the store with the cheapest read that proves it works.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="productId">Optional product to probe.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreVerification> VerifyAsync(string secretJson, string productId, CancellationToken cancellationToken);

    /// <summary>
    /// The owner's products in the store, when the store can list them; otherwise throws a <see cref="StoreCallException"/>
    /// that says how a product is added.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreAppRecord>> ListAppsAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// One product by its external id.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreAppRecord> GetAppAsync(string secretJson, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// The product's versions or packages as builds.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreBuildRecord>> ListBuildsAsync(string secretJson, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// Address of the product's icon, or null when the store has none.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetIconUrlAsync(string secretJson, string externalId, CancellationToken cancellationToken);

    /// <summary>
    /// The job "sync apps" queues for a connection of this store.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="connectionId"></param>
    /// <returns></returns>
    StoreJobRequest SyncJob(int organizationId, int connectionId);

    /// <summary>
    /// Whether a replacement credential belongs to the same account as the stored one (replacing a key must not move a
    /// connection to another account).
    /// </summary>
    /// <param name="current">Fingerprint of the stored credential.</param>
    /// <param name="next">Fingerprint of the replacement.</param>
    /// <returns></returns>
    bool SameAccount(ConnectionFingerprint current, ConnectionFingerprint next);

    /// <summary>
    /// Short, non secret name of a credential for audit texts (a key id, an account).
    /// </summary>
    /// <param name="fingerprint"></param>
    /// <returns></returns>
    string CredentialLabel(ConnectionFingerprint fingerprint);
}
