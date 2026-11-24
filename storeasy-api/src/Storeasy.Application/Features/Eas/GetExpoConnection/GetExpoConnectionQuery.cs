using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetExpoConnection;

/// <summary>
/// The organization's Expo connection: the actor the token belongs to and the last check, never the token. Null without one.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record GetExpoConnectionQuery : IQuery<ExpoConnectionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
