using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.DeliverArtifactSession;

/// <summary>
/// Delivers a held session by hand (design 13.3, 13.18): a member approves what a warning held. The target stays a test
/// channel; production and review submission are never part of intake.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record DeliverArtifactSessionCommand : ICommand<ArtifactSessionDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Session.
    /// </summary>
    public long SessionId { get; set; }
}
