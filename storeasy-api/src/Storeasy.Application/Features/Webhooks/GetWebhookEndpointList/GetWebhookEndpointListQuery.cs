using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEndpointList;

/// <summary>
/// Lists the organization's inbound webhook endpoints with their addresses (owners and admins).
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record GetWebhookEndpointListQuery : IQuery<List<WebhookEndpointDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
