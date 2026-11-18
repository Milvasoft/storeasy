using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSessionList;

/// <summary>
/// Pages through the organization's upload sessions, newest first (design 13.13).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetArtifactSessionListQuery : ListRequest, IListRequestQuery<ArtifactSessionListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Only this state.
    /// </summary>
    public ArtifactSessionStatus? Status { get; set; }

    /// <summary>
    /// Only this source.
    /// </summary>
    public ArtifactSource? Source { get; set; }
}
