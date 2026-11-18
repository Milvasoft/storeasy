using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Webhooks.UpdateOrgWebhook;

/// <summary>
/// Changes an outgoing webhook: name, address (checked again), events, on/off. Null fields stay as they are.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record UpdateOrgWebhookCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Subscription.
    /// </summary>
    public int WebhookId { get; set; }

    /// <summary>
    /// New name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// New address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// New event types (empty: every event).
    /// </summary>
    public List<string> Events { get; set; }

    /// <summary>
    /// On or off.
    /// </summary>
    public bool? IsActive { get; set; }
}
