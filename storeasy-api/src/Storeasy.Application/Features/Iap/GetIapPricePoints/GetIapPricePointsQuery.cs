using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.GetIapPricePoints;

/// <summary>
/// Lists the App Store price points of an In-App Purchase in one territory, live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetIapPricePointsQuery : IQuery<List<PricePointDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the product.
    /// </summary>
    public long IapId { get; set; }

    /// <summary>
    /// Territory (alpha-3).
    /// </summary>
    public string Territory { get; set; }
}
