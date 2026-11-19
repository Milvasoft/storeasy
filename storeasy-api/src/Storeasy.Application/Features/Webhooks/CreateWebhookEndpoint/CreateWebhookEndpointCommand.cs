using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.CreateWebhookEndpoint;

/// <summary>
/// Adds an inbound webhook endpoint: for an App Store Connect connection (one per connection; one webhook per App Store
/// app of the connection, registered at Apple through the API unless <see cref="RegisterAtStore"/> is false), for Google
/// Play notifications (a Pub/Sub push subscription authenticated by <see cref="PushServiceAccount"/>; no secret), or a
/// signed endpoint for any other sender. The secret is returned once.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
[VerifiedEmailRequired]
public record CreateWebhookEndpointCommand : ICommand<WebhookSecretDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Sender.
    /// </summary>
    public WebhookSource Source { get; set; } = WebhookSource.AppStoreConnect;

    /// <summary>
    /// App Store Connect connection (required for <see cref="WebhookSource.AppStoreConnect"/>).
    /// </summary>
    public int? StoreConnectionId { get; set; }

    /// <summary>
    /// Display name (defaults to the connection name).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Registers the endpoint at App Store Connect through the API (a job). False: the address and secret are pasted into
    /// App Store Connect by hand and the first valid event confirms it.
    /// </summary>
    public bool RegisterAtStore { get; set; } = true;

    /// <summary>
    /// Service account e-mail of the Pub/Sub push subscription (required for <see cref="WebhookSource.GooglePlay"/>).
    /// </summary>
    public string PushServiceAccount { get; set; }
}
