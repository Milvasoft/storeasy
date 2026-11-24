using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Eas.SaveExpoConnection;

/// <summary>
/// Saves or replaces the organization's Expo access token (personal or robot). Expo is asked whose token it is first; the token is sealed and never returned.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record SaveExpoConnectionCommand : ICommand<ExpoConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The Expo access token.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }
}
