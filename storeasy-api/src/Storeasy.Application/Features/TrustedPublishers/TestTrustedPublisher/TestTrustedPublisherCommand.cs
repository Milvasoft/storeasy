using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.TrustedPublishers.TestTrustedPublisher;

/// <summary>
/// Checks an OIDC token copied from a CI log against one rule without issuing anything (design 13.7 dry run). The token
/// is not stored.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record TestTrustedPublisherCommand : ICommand<TrustedPublisherTestDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Rule id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// OIDC token.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }
}
