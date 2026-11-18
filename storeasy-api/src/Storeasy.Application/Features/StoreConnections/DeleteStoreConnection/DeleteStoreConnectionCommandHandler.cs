using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.DeleteStoreConnection;

/// <summary>
/// Handles <see cref="DeleteStoreConnectionCommand"/>. Soft delete keeps the row for the audit trail, so the secret
/// columns are emptied first: a deleted connection can never be opened again.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteStoreConnectionCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteStoreConnectionCommand, int>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteStoreConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new { c.Id, c.Store, c.Name },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<int>.Error(0, MessageKey.StoreConnectionNotFound);

        var wipe = new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.EncryptedSecret, [])
                                                            .SetPropertyValue(c => c.EncryptedDataKey, []);

        await _connectionRepository.ExecuteUpdateAsync(connection.Id, wipe, cancellationToken);

        await _connectionRepository.ExecuteDeleteAsync(connection.Id, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ConnectionRemoved,
                                     connection.Id.ToString(),
                                     $"{connection.Store} connection '{connection.Name}' removed.",
                                     connection.Store,
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(connection.Id);
    }
}
