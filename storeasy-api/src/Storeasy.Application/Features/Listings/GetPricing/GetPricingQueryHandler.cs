using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricing;

/// <summary>
/// Handles <see cref="GetPricingQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetPricingQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetPricingQuery, PricingDto>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<PricingDto>> Handle(GetPricingQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Pricing, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<PricingDto>();

        var prefix = Storeasy.Domain.Content.ContentGroups.Availability + "|";

        return Response<PricingDto>.Success(new PricingDto
        {
            StoreAppId = request.StoreAppId,
            BaseTerritory = read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Pricing, null, "baseTerritory")),
            PricePointId = read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Pricing, null, "pricePoint")),
            AvailableInNewTerritories = read.Snapshot.AvailableInNewTerritories,
            Schedule = [.. read.Snapshot.Prices.Select(p => new PriceEntryDto { Territory = p.Territory, PricePointId = p.PricePointId, CustomerPrice = p.CustomerPrice, StartDate = p.StartDate, EndDate = p.EndDate })],
            Territories = [.. read.Snapshot.Values.Where(v => v.Key.StartsWith(prefix, StringComparison.Ordinal))
                                                  .Select(v => new TerritoryDto { Code = Storeasy.Domain.Content.ContentKey.Parse(v.Key).Field, Available = v.Value == "true" })
                                                  .OrderBy(t => t.Code, StringComparer.Ordinal)],
        });
    }
}
