using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.RotateOrgWebhookSecret;

/// <summary>
/// Replaces the signing secret of an outgoing webhook; the new one is returned once and used from the next attempt.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record RotateOrgWebhookSecretCommand : ICommand<WebhookSecretDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Subscription.
    /// </summary>
    public int WebhookId { get; set; }
}
