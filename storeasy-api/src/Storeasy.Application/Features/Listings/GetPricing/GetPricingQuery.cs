using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetPricing;

/// <summary>
/// Reads the price schedule and territory availability of an App Store record live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetPricingQuery : IQuery<PricingDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }
}
