using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEventDetail;

/// <summary>
/// Reads one inbound event with its raw body and masked signature header.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record GetWebhookEventDetailQuery : IQuery<WebhookEventDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Event.
    /// </summary>
    public long EventId { get; set; }
}
