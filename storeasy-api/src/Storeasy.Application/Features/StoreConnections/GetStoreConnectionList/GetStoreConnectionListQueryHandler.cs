using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreConnections.GetStoreConnectionList;

/// <summary>
/// Handles <see cref="GetStoreConnectionListQuery"/>.
/// </summary>
/// <param name="connectionRepository"></param>
public class GetStoreConnectionListQueryHandler(IStoreasyRepositoryBase<StoreConnection> connectionRepository) : IInterceptable, IQueryHandler<GetStoreConnectionListQuery, List<StoreConnectionDto>>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;

    /// <inheritdoc/>
    public async Task<Response<List<StoreConnectionDto>>> Handle(GetStoreConnectionListQuery request, CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetAllAsync(c => c.OrganizationId == request.OrganizationId,
                                                                  projection: StoreConnectionDto.Projection,
                                                                  cancellationToken: cancellationToken);

        return Response<List<StoreConnectionDto>>.Success([.. connections.OrderBy(c => c.Store).ThenBy(c => c.Name)]);
    }
}
