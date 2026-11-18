using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapDetail;

/// <summary>
/// Handles <see cref="GetIapDetailQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetIapDetailQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IQueryHandler<GetIapDetailQuery, ProductDetailDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public Task<Response<ProductDetailDto>> Handle(GetIapDetailQuery request, CancellationToken cancellationToken)
    {
        return _productRepository.GetProductDetailAsync(request.OrganizationId, request.IapId, [StoreProductKind.Consumable, StoreProductKind.NonConsumable, StoreProductKind.NonRenewingSubscription, StoreProductKind.OneTimeProduct], cancellationToken);
    }
}
