using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSessionDetail;

/// <summary>
/// Reads one upload session with its steps, package, checks and CI result (tools/CONTRACT.md 3.4).
/// </summary>
[OrgAuth(OrgPermission.View)]
[AllowMachineToken]
public record GetArtifactSessionDetailQuery : IQuery<ArtifactSessionDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Session.
    /// </summary>
    public long SessionId { get; set; }
}
