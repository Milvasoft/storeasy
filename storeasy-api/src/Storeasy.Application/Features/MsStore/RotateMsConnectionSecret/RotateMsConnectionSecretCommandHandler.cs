using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Stores;
using System.Text;

namespace Storeasy.Application.Features.MsStore.RotateMsConnectionSecret;

/// <summary>
/// Handles <see cref="RotateMsConnectionSecretCommand"/>. The secret property is <c>[LogMasked]</c>. The new secret is
/// checked offline, compared with the connection's identifiers, verified against Microsoft and only then sealed; the
/// update writes the sealed secret, the fingerprint, the expiry and the verification result in one statement.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreGateway"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record RotateMsConnectionSecretCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                                     IStoreGateway StoreGateway,
                                                     ISecretProtector SecretProtector,
                                                     IAuditTrail AuditTrail,
                                                     TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RotateMsConnectionSecretCommand, StoreConnectionDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreGateway _storeGateway = StoreGateway;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<StoreConnectionDto>> Handle(RotateMsConnectionSecretCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId && c.Store == StoreKind.Microsoft,
                                                                            projection: c => new StoreConnection { Id = c.Id, Name = c.Name, Fingerprint = c.Fingerprint },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreConnectionNotFound);

        var inspection = _storeGateway.Inspect(StoreKind.Microsoft, request.Secret);

        if (!inspection.IsValid)
            return Response<StoreConnectionDto>.Error(null, inspection.ErrorKey);

        var current = connection.Fingerprint;
        var next = inspection.Fingerprint;

        if (current == null || current.TenantIdSuffix != next.TenantIdSuffix || current.ClientIdSuffix != next.ClientIdSuffix || current.SellerId != next.SellerId)
            return Response<StoreConnectionDto>.Error(null, MessageKey.MsSecretMismatch);

        var verification = await _storeGateway.VerifyAsync(StoreKind.Microsoft, inspection.NormalizedSecret, null, cancellationToken);

        if (verification.Status != ConnectionStatus.Verified)
            return Response<StoreConnectionDto>.Error(null, MessageKey.MsSecretRejected);

        next.SecretExpiryDate = request.CredentialExpiryDate;

        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(inspection.NormalizedSecret), StoreSecretContext.Connection(request.OrganizationId, StoreKind.Microsoft));
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var builder = new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.EncryptedSecret, sealedSecret.EncryptedSecret)
                                                               .SetPropertyValue(c => c.EncryptedDataKey, sealedSecret.EncryptedDataKey)
                                                               .SetPropertyValue(c => c.KeyVersion, sealedSecret.KeyVersion)
                                                               .SetPropertyValue(c => c.Fingerprint, next)
                                                               .SetPropertyValue(c => c.SecretExpiryDate, request.CredentialExpiryDate)
                                                               .SetPropertyValue(c => c.Status, ConnectionStatus.Verified)
                                                               .SetPropertyValue(c => c.LastVerifiedDate, (DateTime?)now)
                                                               .SetPropertyValue(c => c.LastVerifyError, (string)null);

        await _connectionRepository.ExecuteUpdateAsync(connection.Id, builder, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MsConnectionSecretRotated,
                                     connection.Id.ToString(),
                                     $"Microsoft connection '{connection.Name}' secret replaced; expires {request.CredentialExpiryDate:yyyy-MM-dd}.",
                                     StoreKind.Microsoft,
                                     next,
                                     cancellationToken);

        var dto = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connection.Id, projection: StoreConnectionDto.Projection, cancellationToken: cancellationToken);

        return Response<StoreConnectionDto>.Success(dto);
    }
}
