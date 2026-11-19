using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.UpdateStoreConnection;

/// <summary>
/// Handles <see cref="UpdateStoreConnectionCommand"/>. A store logo must be the connection's own store's; a change writes
/// the name and the icon in one statement and is audited, a request that changes nothing writes and audits nothing.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateStoreConnectionCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                                  IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateStoreConnectionCommand, StoreConnectionDto>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<StoreConnectionDto>> Handle(UpdateStoreConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new StoreConnection { Id = c.Id, Store = c.Store, Name = c.Name, Icon = c.Icon },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreConnectionNotFound);

        var name = request.Name.Trim();
        var icon = StoreConnectionIcons.Normalize(request.Icon);

        if (!StoreConnectionIcons.IsAllowed(connection.Store, icon))
            return Response<StoreConnectionDto>.Error(null, MessageKey.StoreConnectionIconInvalid);

        if (name != connection.Name || icon != connection.Icon)
        {
            var builder = new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.Name, name)
                                                                   .SetPropertyValue(c => c.Icon, icon);

            await _connectionRepository.ExecuteUpdateAsync(connection.Id, builder, cancellationToken);

            await _auditTrail.WriteAsync(request.OrganizationId,
                                         AuditAction.ConnectionUpdated,
                                         connection.Id.ToString(),
                                         $"{connection.Store} connection '{connection.Name}' updated: name '{name}', icon {icon ?? "none"}.",
                                         connection.Store,
                                         new { Name = name, Icon = icon, PreviousName = connection.Name, PreviousIcon = connection.Icon },
                                         cancellationToken);
        }

        var dto = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == connection.Id, projection: StoreConnectionDto.Projection, cancellationToken: cancellationToken);

        return Response<StoreConnectionDto>.Success(dto);
    }
}
