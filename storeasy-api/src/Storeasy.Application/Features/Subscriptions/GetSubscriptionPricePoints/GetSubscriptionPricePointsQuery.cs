using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionPricePoints;

/// <summary>
/// Lists the App Store price points of a subscription in one territory, live; prices are only ever chosen from them.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSubscriptionPricePointsQuery : IQuery<List<PricePointDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the subscription.
    /// </summary>
    public long SubscriptionId { get; set; }

    /// <summary>
    /// Territory (alpha-3).
    /// </summary>
    public string Territory { get; set; }
}
