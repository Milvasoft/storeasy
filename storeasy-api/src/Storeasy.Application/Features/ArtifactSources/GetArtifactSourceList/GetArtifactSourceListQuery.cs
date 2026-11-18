using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.ArtifactSources.GetArtifactSourceList;

/// <summary>
/// Lists the pull sources (EAS and generic webhooks) of the organization (design 13.19). Secrets are never returned.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetArtifactSourceListQuery : IQuery<List<ArtifactSourceDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only this app.
    /// </summary>
    public int? AppId { get; set; }
}
