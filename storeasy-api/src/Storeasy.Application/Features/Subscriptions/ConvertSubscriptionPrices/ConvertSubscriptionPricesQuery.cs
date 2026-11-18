using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.ConvertSubscriptionPrices;

/// <summary>
/// Asks Play to convert a price to every region (Play convertRegionPrices); nothing is saved, the result is added to a plan by the member.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record ConvertSubscriptionPricesQuery : IQuery<ConvertedPricesDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (Google Play).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Currency of the amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Amount with a period as decimal separator.
    /// </summary>
    public string Amount { get; set; }
}
