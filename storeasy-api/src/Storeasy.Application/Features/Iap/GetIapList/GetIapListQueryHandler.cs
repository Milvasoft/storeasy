using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Utils.Monetization;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapList;

/// <summary>
/// Handles <see cref="GetIapListQuery"/>.
/// </summary>
/// <param name="productRepository"></param>
public class GetIapListQueryHandler(IStoreasyRepositoryBase<StoreProduct> productRepository) : IInterceptable, IListQueryHandler<GetIapListQuery, IapListDto>
{
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = productRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<IapListDto>> Handle(GetIapListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);

        var search = request.Search?.Trim();
        var response = await _productRepository.GetAllAsync(request,
                                                            condition: p => p.OrganizationId == request.OrganizationId
                                                                            && p.StoreAppId == request.StoreAppId
                                                                            && p.Kind != StoreProductKind.Subscription
                                                                            && (request.Kind == null || p.Kind == request.Kind)
                                                                            && (request.State == null || p.State == request.State)
                                                                            && (string.IsNullOrEmpty(search) || p.ProductId.Contains(search) || p.ReferenceName.Contains(search)),
                                                            projection: IapListDto.Projection,
                                                            cancellationToken: cancellationToken);

        foreach (var row in response.Data ?? [])
        {
            row.BasePrice = MonetizationViews.BasePrice(row.BaseTerritory, row.Prices?.Count > 0 ? row.Prices : row.Plans?.FirstOrDefault()?.Prices);
            row.PurchaseOptions = row.Plans?.Count > 0 ? [.. row.Plans.Select(p => MonetizationViews.Plan(p, null))] : null;
        }

        return response;
    }
}
