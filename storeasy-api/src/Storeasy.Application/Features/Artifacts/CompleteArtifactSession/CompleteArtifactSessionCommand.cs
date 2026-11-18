using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.CompleteArtifactSession;

/// <summary>
/// Completes the multipart upload of a session and starts processing (tools/CONTRACT.md 3.3). Idempotent.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
[AllowMachineToken]
public record CompleteArtifactSessionCommand : ICommand<ArtifactSessionListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Session (from the route).
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// Every part with the ETag the storage returned, ascending.
    /// </summary>
    public List<ArtifactUploadedPartDto> Parts { get; set; } = [];
}
