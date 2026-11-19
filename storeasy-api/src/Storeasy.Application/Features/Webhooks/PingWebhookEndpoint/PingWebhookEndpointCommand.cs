using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Webhooks.PingWebhookEndpoint;

/// <summary>
/// Asks App Store Connect to send a test event (ping) to every webhook of an App Store Connect endpoint, through a job.
/// The pings arrive at the receiver like real events and show in the event log with their signature verdict, so this
/// checks the address, the secret and the proxy end to end. Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record PingWebhookEndpointCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Endpoint.
    /// </summary>
    public int EndpointId { get; set; }
}
