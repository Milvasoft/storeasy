using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanPricing;

/// <summary>
/// Plans the base price and territory availability of an App Store record (PUT pricing).
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanPricingCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Base territory of the price (TUR, USA...).
    /// </summary>
    public string BaseTerritory { get; set; }

    /// <summary>
    /// Price point of the base territory; null keeps the price.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Territories to make available or unavailable.
    /// </summary>
    public List<TerritoryDto> Territories { get; set; } = [];

    /// <summary>
    /// Only make the plan (default). False approves it right away with the same checks as an approval.
    /// </summary>
    public bool DryRun { get; set; } = true;
}
