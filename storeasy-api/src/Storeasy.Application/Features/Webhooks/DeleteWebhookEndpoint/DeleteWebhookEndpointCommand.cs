using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Webhooks.DeleteWebhookEndpoint;

/// <summary>
/// Removes an inbound endpoint; its App Store Connect webhooks are deleted by a job. Its events stay in the log.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record DeleteWebhookEndpointCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Endpoint.
    /// </summary>
    public int EndpointId { get; set; }
}
