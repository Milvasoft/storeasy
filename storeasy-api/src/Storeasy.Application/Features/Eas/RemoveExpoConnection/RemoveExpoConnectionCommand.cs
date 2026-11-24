using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.RemoveExpoConnection;

/// <summary>
/// Removes the organization's Expo access token.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record RemoveExpoConnectionCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
