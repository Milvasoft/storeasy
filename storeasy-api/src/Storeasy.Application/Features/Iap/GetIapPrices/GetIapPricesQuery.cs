using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPrices;

/// <summary>
/// Lists the territory prices of an In-App Purchase (of one Play purchase option) as last synced; searched, filtered and paged.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetIapPricesQuery : ListRequest, IListRequestQuery<ProductPriceDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the product.
    /// </summary>
    public long IapId { get; set; }

    /// <summary>
    /// Purchase option (Play); null for App Store prices.
    /// </summary>
    public string PlanId { get; set; }

    /// <summary>
    /// Search in territory names.
    /// </summary>
    public string TerritoryName { get; set; }

    /// <summary>
    /// Only prices of this source.
    /// </summary>
    public PriceSource? Source { get; set; }

    /// <summary>
    /// Only territories with (or without) a scheduled change.
    /// </summary>
    public bool? HasPendingChange { get; set; }
}
