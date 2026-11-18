using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EventDtos;

namespace Storeasy.Application.Features.Webhooks.RotateWebhookSecret;

/// <summary>
/// Replaces the secret of an inbound endpoint. The old secret stays valid for <c>Storeasy:Webhooks:SecretGraceHours</c>
/// (24 by default); an endpoint registered through the API gets the new secret at App Store Connect by a job.
/// </summary>
[OrgAuth(OrgPermission.ManageWebhooks)]
public record RotateWebhookSecretCommand : ICommand<WebhookSecretDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Endpoint.
    /// </summary>
    public int EndpointId { get; set; }
}
