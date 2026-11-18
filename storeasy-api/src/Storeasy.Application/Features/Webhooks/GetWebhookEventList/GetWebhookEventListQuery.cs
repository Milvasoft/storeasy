using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEventList;

/// <summary>
/// Pages through the organization's inbound webhook events, newest first (filters on the DTO fields: endpoint, event type,
/// signature, result).
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record GetWebhookEventListQuery : ListRequest, IListRequestQuery<WebhookEventDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
