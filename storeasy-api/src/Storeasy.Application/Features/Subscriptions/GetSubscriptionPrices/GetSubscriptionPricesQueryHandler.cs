using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionPrices;

/// <summary>
/// Handles <see cref="GetSubscriptionPricesQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetSubscriptionPricesQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IListQueryHandler<GetSubscriptionPricesQuery, ProductPriceDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public Task<ListResponse<ProductPriceDto>> Handle(GetSubscriptionPricesQuery request, CancellationToken cancellationToken)
    {
        return _productRepository.GetPricesAsync(request, request.OrganizationId, request.SubscriptionId, request.PlanId, request.TerritoryName, request.Source, request.HasPendingChange, cancellationToken);
    }
}
