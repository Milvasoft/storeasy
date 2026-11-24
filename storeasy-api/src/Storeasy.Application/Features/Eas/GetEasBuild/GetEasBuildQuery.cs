using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasBuild;

/// <summary>
/// One EAS build with its state, versions, commit, links and error.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasBuildQuery : IQuery<EasBuildDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy build id.
    /// </summary>
    public long Id { get; set; }
}
