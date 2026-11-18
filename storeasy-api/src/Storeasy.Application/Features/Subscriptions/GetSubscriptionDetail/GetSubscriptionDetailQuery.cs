using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionDetail;

/// <summary>
/// Reads a subscription (App Store) or Play subscription as last synced.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSubscriptionDetailQuery : IQuery<ProductDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy id of the subscription.
    /// </summary>
    public long SubscriptionId { get; set; }
}
