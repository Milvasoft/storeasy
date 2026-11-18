using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Subscriptions.GetSubscriptionGroups;

/// <summary>
/// Lists the subscriptions of a store record as last synced: App Store groups in level order, or Play subscriptions with their base plans and offers.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetSubscriptionGroupsQuery : IQuery<SubscriptionGroupsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
