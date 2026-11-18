using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPricePoints;

/// <summary>
/// Handles <see cref="GetIapPricePointsQuery"/>.
/// </summary>
/// <param name="monetizationService"></param>
/// <param name="contentService"></param>
/// <param name="productRepository"></param>
public class GetIapPricePointsQueryHandler(IMonetizationService monetizationService,
                                           IStoreContentService contentService,
                                           IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IQueryHandler<GetIapPricePointsQuery, List<PricePointDto>>
{
    private readonly IMonetizationService _monetizationService = monetizationService;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public Task<Response<List<PricePointDto>>> Handle(GetIapPricePointsQuery request, CancellationToken cancellationToken)
    {
        return _monetizationService.GetPricePointsAsync(_contentService, _productRepository, request.OrganizationId, request.IapId, request.Territory, cancellationToken);
    }
}
