using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionOffers;

/// <summary>
/// Handles <see cref="GetSubscriptionOffersQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetSubscriptionOffersQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IQueryHandler<GetSubscriptionOffersQuery, SubscriptionOffersDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public async Task<Response<SubscriptionOffersDto>> Handle(GetSubscriptionOffersQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetFirstOrDefaultAsync(p => p.Id == request.SubscriptionId && p.OrganizationId == request.OrganizationId && p.Kind == StoreProductKind.Subscription,
                                                                      projection: p => new StoreProduct { Id = p.Id, Offers = p.Offers },
                                                                      cancellationToken: cancellationToken);

        if (product == null)
            return Response<SubscriptionOffersDto>.Error(null, MessageKey.MonetizationProductNotFound);

        return Response<SubscriptionOffersDto>.Success(MonetizationViews.Offers(product.Id, product.Offers));
    }
}
