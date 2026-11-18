using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Webhooks.DeleteOrgWebhook;

/// <summary>
/// Removes an outgoing webhook; pending deliveries stop.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record DeleteOrgWebhookCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Subscription.
    /// </summary>
    public int WebhookId { get; set; }
}
