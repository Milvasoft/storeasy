using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.CiTokens.RevokeCiToken;

/// <summary>
/// Revokes a CI token at once (design 13.11): new sessions with it are refused (401 TokenRevoked).
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record RevokeCiTokenCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// CI token id.
    /// </summary>
    public int Id { get; set; }
}
