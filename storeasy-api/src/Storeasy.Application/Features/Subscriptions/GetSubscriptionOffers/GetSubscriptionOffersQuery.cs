using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionOffers;

/// <summary>
/// Lists the offers of a subscription by kind, as last synced.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSubscriptionOffersQuery : IQuery<SubscriptionOffersDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the subscription.
    /// </summary>
    public long SubscriptionId { get; set; }
}
