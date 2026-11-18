using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Features.MsStore.GetMsAddonList;

/// <summary>
/// Handles <see cref="GetMsAddonListQuery"/>. A record of another organization or another store is reported as not found.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="addonRepository"></param>
public class GetMsAddonListQueryHandler(IStoreasyRepositoryBase<MsProduct> productRepository, IStoreasyRepositoryBase<MsAddon> addonRepository) : IInterceptable, IQueryHandler<GetMsAddonListQuery, List<MsAddonDto>>
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = addonRepository;

    /// <inheritdoc/>
    public async Task<Response<List<MsAddonDto>>> Handle(GetMsAddonListQuery request, CancellationToken cancellationToken)
    {
        if (!await _productRepository.MsProductExistsAsync(request.OrganizationId, request.StoreAppId, cancellationToken))
            return Response<List<MsAddonDto>>.Error(null, MessageKey.StoreAppNotFound);

        var items = await _addonRepository.GetAllAsync(x => x.StoreAppId == request.StoreAppId && x.OrganizationId == request.OrganizationId,
                                                       projection: MsAddonDto.Projection,
                                                       cancellationToken: cancellationToken);

        return Response<List<MsAddonDto>>.Success([.. items.OrderBy(a => a.ProductId)]);
    }
}
