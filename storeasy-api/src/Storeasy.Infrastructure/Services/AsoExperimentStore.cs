using Milvasoft.Core.Abstractions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Stores;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IAsoExperimentStore"/>: opens the record's connection secret for one read (the decrypted bytes are
/// cleared at once) and reads through the store's <see cref="IAsoExperimentGateway"/>. Store failures are logged
/// (redacted) and returned as <see cref="MessageKey.StoreUnreachable"/> (transient) or <see cref="MessageKey.StoreRejected"/>.
/// </summary>
/// <param name="gateways"></param>
/// <param name="connectionRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="logger"></param>
public sealed class AsoExperimentStore(IEnumerable<IAsoExperimentGateway> gateways,
                                       IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                       ISecretProtector secretProtector,
                                       IMilvaLogger logger) : IAsoExperimentStore
{
    private readonly IEnumerable<IAsoExperimentGateway> _gateways = gateways;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task<AsoStoreReadResult> ReadAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        var gateway = _gateways.LastOrDefault(g => g.Store == app.Store);

        if (gateway == null)
            return new AsoStoreReadResult(null, MessageKey.AsoExperimentNoApi);

        var (target, errorKey) = await OpenAsync(app, cancellationToken);

        if (errorKey != null)
            return new AsoStoreReadResult(null, errorKey);

        try
        {
            return new AsoStoreReadResult(await gateway.ReadAsync(target, cancellationToken));
        }
        catch (StoreCallException ex)
        {
            _logger?.Warning("Experiment read of {Store} record {StoreAppId} failed: {Error}", app.Store, app.StoreAppId, SecretRedactor.Redact(ex.Message));

            return new AsoStoreReadResult(null, ex.Transient ? MessageKey.StoreUnreachable : MessageKey.StoreRejected, SecretRedactor.RedactAndTrim(ex.Message, 300));
        }
    }

    private async Task<(StoreContentTarget Target, string ErrorKey)> OpenAsync(StoreAppContext app, CancellationToken cancellationToken)
    {
        if (app.ConnectionId == null)
            return (null, MessageKey.StoreAppNotConnected);

        var sealedSecret = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == app.ConnectionId && c.OrganizationId == app.OrganizationId,
                                                                              projection: c => new StoreConnection { Store = c.Store, EncryptedSecret = c.EncryptedSecret, EncryptedDataKey = c.EncryptedDataKey, KeyVersion = c.KeyVersion },
                                                                              cancellationToken: cancellationToken);

        if (sealedSecret?.EncryptedSecret is not { Length: > 0 })
            return (null, MessageKey.StoreAppNotConnected);

        byte[] plain = null;

        try
        {
            plain = _secretProtector.Unprotect(new ProtectedSecret(sealedSecret.EncryptedSecret, sealedSecret.EncryptedDataKey, sealedSecret.KeyVersion), StoreSecretContext.Connection(app.OrganizationId, sealedSecret.Store));

            return (new StoreContentTarget(Encoding.UTF8.GetString(plain), app.ExternalId), null);
        }
        catch (CryptographicException)
        {
            return (null, MessageKey.StoreAppNotConnected);
        }
        finally
        {
            if (plain != null)
                CryptographicOperations.ZeroMemory(plain);
        }
    }
}
