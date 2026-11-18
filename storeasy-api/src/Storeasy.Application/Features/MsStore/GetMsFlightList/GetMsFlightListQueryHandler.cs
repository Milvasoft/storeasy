using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsFlightList;

/// <summary>
/// Handles <see cref="GetMsFlightListQuery"/>. A record of another organization or another store is reported as not found.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="flightRepository"></param>
/// <param name="groupRepository"></param>
public class GetMsFlightListQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository, IStoreasyRepositoryBase<MsFlight> flightRepository, IStoreasyRepositoryBase<MsFlightGroup> groupRepository) : IInterceptable, IQueryHandler<GetMsFlightListQuery, List<MsFlightDto>>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = flightRepository;
    private readonly IStoreasyRepositoryBase<MsFlightGroup> _groupRepository = groupRepository;

    /// <inheritdoc/>
    public async Task<Response<List<MsFlightDto>>> Handle(GetMsFlightListQuery request, CancellationToken cancellationToken)
    {
        if (!await _productRepository.MsProductExistsAsync(request.OrganizationId, request.StoreAppId, cancellationToken))
            return Response<List<MsFlightDto>>.Error(null, MessageKey.StoreAppNotFound);

        var items = await _flightRepository.GetAllAsync(x => x.StoreAppId == request.StoreAppId && x.OrganizationId == request.OrganizationId,
                                                        projection: MsFlightDto.Projection,
                                                        cancellationToken: cancellationToken);

        var groupIds = items.SelectMany(f => f.GroupIds ?? []).Distinct().ToList();
        var groups = groupIds.Count == 0
            ? []
            : await _groupRepository.GetAllAsync(g => g.OrganizationId == request.OrganizationId && groupIds.Contains(g.GroupId), projection: MsFlightGroupDto.Projection, cancellationToken: cancellationToken);

        foreach (var flight in items)
            flight.Groups = [.. (flight.GroupIds ?? []).Select(id => groups.FirstOrDefault(g => g.Id == id) ?? new MsFlightGroupDto { Id = id })];

        return Response<List<MsFlightDto>>.Success([.. items.OrderBy(f => f.Rank ?? int.MaxValue).ThenBy(f => f.FriendlyName)]);
    }
}
