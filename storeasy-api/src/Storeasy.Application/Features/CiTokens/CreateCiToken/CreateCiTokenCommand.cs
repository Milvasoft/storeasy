using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.CiTokens.CreateCiToken;

/// <summary>
/// Creates a CI token (sci_) of the organization for some apps or all (design 13.9). The value is returned once.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record CreateCiTokenCommand : ICommand<CiTokenCreatedDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name (unique among the organization's active CI tokens).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Apps the token may upload to.
    /// </summary>
    public List<int> AppIds { get; set; } = [];

    /// <summary>
    /// Whether the token may upload to every app.
    /// </summary>
    public bool AllApps { get; set; }

    /// <summary>
    /// Scopes; today only artifacts:write.
    /// </summary>
    public List<string> Scopes { get; set; } = ["artifacts:write"];

    /// <summary>
    /// Lifetime in days (at most 365).
    /// </summary>
    public int ExpiryDays { get; set; } = 90;
}
