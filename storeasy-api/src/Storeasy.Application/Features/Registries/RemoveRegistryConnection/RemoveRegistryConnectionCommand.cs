using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Registries.RemoveRegistryConnection;

/// <summary>
/// Removes a credential of the package registries module (and the token reminder kept for it).
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record RemoveRegistryConnectionCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Credential.
    /// </summary>
    public int Id { get; set; }
}
