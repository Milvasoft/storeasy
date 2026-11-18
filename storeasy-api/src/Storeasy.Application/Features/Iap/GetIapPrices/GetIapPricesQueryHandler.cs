using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPrices;

/// <summary>
/// Handles <see cref="GetIapPricesQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetIapPricesQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IListQueryHandler<GetIapPricesQuery, ProductPriceDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public Task<ListResponse<ProductPriceDto>> Handle(GetIapPricesQuery request, CancellationToken cancellationToken)
    {
        return _productRepository.GetPricesAsync(request, request.OrganizationId, request.IapId, request.PlanId, request.TerritoryName, request.Source, request.HasPendingChange, cancellationToken);
    }
}
