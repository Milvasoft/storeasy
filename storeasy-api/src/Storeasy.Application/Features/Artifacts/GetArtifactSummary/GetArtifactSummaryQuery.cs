using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSummary;

/// <summary>
/// The last package of every app with its delivery rules (design 13.1 "Uygulama başına son paket").
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetArtifactSummaryQuery : IQuery<List<ArtifactAppSummaryDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
