using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.CreateOrgWebhook;

/// <summary>
/// Subscribes an HTTPS address to the organization's events. The address must resolve to public addresses only (SSRF
/// protection). The signing secret is returned once.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
[VerifiedEmailRequired]
public record CreateOrgWebhookCommand : ICommand<WebhookSecretDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// HTTPS address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Event types (<c>rejection.opened</c>...); empty for every event.
    /// </summary>
    public List<string> Events { get; set; } = [];
}
