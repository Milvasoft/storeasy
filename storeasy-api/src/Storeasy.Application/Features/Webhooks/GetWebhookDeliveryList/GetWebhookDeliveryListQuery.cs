using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.GetWebhookDeliveryList;

/// <summary>
/// Pages through the delivery log of the organization's outgoing webhooks, newest first (filters on the DTO fields).
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record GetWebhookDeliveryListQuery : ListRequest, IListRequestQuery<WebhookDeliveryDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the deliveries of this subscription (optional).
    /// </summary>
    public int? WebhookId { get; set; }
}
