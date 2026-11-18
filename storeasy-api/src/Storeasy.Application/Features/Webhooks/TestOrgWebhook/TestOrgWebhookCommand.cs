using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.TestOrgWebhook;

/// <summary>
/// Posts a signed <c>webhook.test</c> event to an outgoing webhook right away and returns the attempt (status, answer,
/// duration). The attempt is kept in the delivery log; a failed test is not retried.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record TestOrgWebhookCommand : ICommand<WebhookDeliveryDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Subscription.
    /// </summary>
    public int WebhookId { get; set; }
}
