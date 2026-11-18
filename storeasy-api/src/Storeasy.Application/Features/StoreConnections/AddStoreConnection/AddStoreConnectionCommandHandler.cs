using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Stores;
using System.Text;

namespace Storeasy.Application.Features.StoreConnections.AddStoreConnection;

/// <summary>
/// Handles <see cref="AddStoreConnectionCommand"/>. The secret property is <c>[LogMasked]</c>.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreGateway"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
[Log]
public record AddStoreConnectionCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                               IStoreGateway StoreGateway,
                                               ISecretProtector SecretProtector,
                                               IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<AddStoreConnectionCommand, int>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreGateway _storeGateway = StoreGateway;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(AddStoreConnectionCommand request, CancellationToken cancellationToken)
    {
        var inspection = _storeGateway.Inspect(request.Store, request.Secret);

        if (!inspection.IsValid)
            return Response<int>.Error(0, inspection.ErrorKey);

        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(inspection.NormalizedSecret), StoreSecretContext.Connection(request.OrganizationId, request.Store));

        var connection = new StoreConnection
        {
            OrganizationId = request.OrganizationId,
            Store = request.Store,
            Name = request.Name.Trim(),
            Fingerprint = inspection.Fingerprint,
            EncryptedSecret = sealedSecret.EncryptedSecret,
            EncryptedDataKey = sealedSecret.EncryptedDataKey,
            KeyVersion = sealedSecret.KeyVersion,
            Status = ConnectionStatus.NotVerified,
        };

        await _connectionRepository.AddAsync(connection, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ConnectionAdded,
                                     connection.Id.ToString(),
                                     $"{request.Store} connection '{connection.Name}' added.",
                                     request.Store,
                                     inspection.Fingerprint,
                                     cancellationToken);

        return Response<int>.Success(connection.Id);
    }
}
