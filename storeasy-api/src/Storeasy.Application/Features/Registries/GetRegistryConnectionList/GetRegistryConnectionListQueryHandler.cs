using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetRegistryConnectionList;

/// <summary>
/// Handles <see cref="GetRegistryConnectionListQuery"/>.
/// </summary>
/// <param name="connectionRepository"></param>
public class GetRegistryConnectionListQueryHandler(IStoreasyRepositoryBase<RegistryConnection> connectionRepository) : IInterceptable, IQueryHandler<GetRegistryConnectionListQuery, List<RegistryConnectionDto>>
{
    private readonly IStoreasyRepositoryBase<RegistryConnection> _connectionRepository = connectionRepository;

    /// <inheritdoc/>
    public async Task<Response<List<RegistryConnectionDto>>> Handle(GetRegistryConnectionListQuery request, CancellationToken cancellationToken)
    {
        var items = await _connectionRepository.GetAllAsync(c => c.OrganizationId == request.OrganizationId, projection: RegistryConnectionDto.Projection, cancellationToken: cancellationToken);

        return Response<List<RegistryConnectionDto>>.Success([.. items.OrderBy(c => c.Kind)]);
    }
}
