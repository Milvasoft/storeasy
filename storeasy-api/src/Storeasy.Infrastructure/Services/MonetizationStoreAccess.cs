using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Opens a store record's connection for one monetization call and reads its products. Kept apart from
/// <see cref="MonetizationService"/> so the change plan extension and the readiness contributor can read without
/// depending on the planning service (which itself depends on the change plan service).
/// </summary>
/// <param name="gateways"></param>
/// <param name="connectionRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="logger"></param>
public sealed class MonetizationStoreAccess(IEnumerable<IMonetizationGateway> gateways,
                                            IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                            ISecretProtector secretProtector,
                                            IMilvaLogger logger)
{
    private readonly IEnumerable<IMonetizationGateway> _gateways = gateways;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IMilvaLogger _logger = logger;

    /// <summary>
    /// Reads the record's products live; failures come back as a message key.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="parts"></param>
    /// <param name="productIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<MonetizationRead> ReadAsync(StoreAppContext app, MonetizationParts parts, IReadOnlyCollection<string> productIds, CancellationToken cancellationToken)
    {
        var (gateway, target, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return new MonetizationRead(null, errorKey);

        try
        {
            return new MonetizationRead(await gateway.ReadAsync(target, parts, productIds, cancellationToken));
        }
        catch (StoreCallException ex)
        {
            var (key, detail) = Failure(app, ex);

            return new MonetizationRead(null, key, detail);
        }
    }

    /// <summary>
    /// The store's gateway and a target with the connection secret opened (the decrypted bytes are cleared at once).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(IMonetizationGateway Gateway, StoreContentTarget Target, string ErrorKey)> OpenAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == app.Store);

        if (gateway == null)
            return (null, null, MessageKey.StoreNotSupported);

        if (app.ConnectionId == null)
            return (null, null, MessageKey.StoreAppNotConnected);

        var sealedSecret = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == app.ConnectionId && c.OrganizationId == app.OrganizationId,
                                                                              projection: c => new StoreConnection { Store = c.Store, EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                              cancellationToken: cancellationToken);

        if (sealedSecret?.EncryptedSecret is not { Length: > 0 })
            return (null, null, MessageKey.StoreAppNotConnected);

        byte[] plain = null;

        try
        {
            plain = _secretProtector.Unprotect(new ProtectedSecret(sealedSecret.EncryptedSecret, sealedSecret.EncryptedDataKey, sealedSecret.KeyVersion), StoreSecretContext.Connection(app.OrganizationId, sealedSecret.Store));

            return (gateway, new StoreContentTarget(Encoding.UTF8.GetString(plain), app.ExternalId), null);
        }
        catch (CryptographicException)
        {
            return (null, null, MessageKey.StoreAppNotConnected);
        }
        finally
        {
            if (plain != null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>
    /// Logs a failed store call (redacted) and maps it to a message key and a redacted detail.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    public (string Key, string Detail) Failure(StoreAppContext app, StoreCallException exception)
    {
        _logger?.Warning("Monetization call of {Store} record {StoreAppId} failed: {Error}", app.Store, app.StoreAppId, SecretRedactor.Redact(exception.Message));

        return (exception.Transient ? MessageKey.StoreUnreachable : MessageKey.StoreRejected, SecretRedactor.RedactAndTrim(exception.Message, 300));
    }
}
