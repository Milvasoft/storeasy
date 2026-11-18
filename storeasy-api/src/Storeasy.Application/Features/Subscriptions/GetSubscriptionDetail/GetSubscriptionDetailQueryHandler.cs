using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionDetail;

/// <summary>
/// Handles <see cref="GetSubscriptionDetailQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetSubscriptionDetailQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IQueryHandler<GetSubscriptionDetailQuery, ProductDetailDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public Task<Response<ProductDetailDto>> Handle(GetSubscriptionDetailQuery request, CancellationToken cancellationToken)
    {
        return _productRepository.GetProductDetailAsync(request.OrganizationId, request.SubscriptionId, [StoreProductKind.Subscription], cancellationToken);
    }
}
