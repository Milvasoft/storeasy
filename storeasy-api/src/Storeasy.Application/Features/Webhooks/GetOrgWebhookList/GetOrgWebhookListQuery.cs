using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.GetOrgWebhookList;

/// <summary>
/// Lists the organization's outgoing webhook subscriptions (owners and admins).
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record GetOrgWebhookListQuery : IQuery<List<OrgWebhookDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
