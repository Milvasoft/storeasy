using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.ArtifactSources.DeleteArtifactSource;

/// <summary>
/// Removes a pull source; its webhook URL stops working at once.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record DeleteArtifactSourceCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Source id.
    /// </summary>
    public int Id { get; set; }
}
