using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Artifacts.DeleteArtifactSession;

/// <summary>
/// Deletes the stored package of a finished session now instead of at the end of the retention (design 13.16).
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record DeleteArtifactSessionCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Session.
    /// </summary>
    public long SessionId { get; set; }
}
