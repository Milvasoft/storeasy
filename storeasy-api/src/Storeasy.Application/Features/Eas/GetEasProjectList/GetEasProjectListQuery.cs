using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasProjectList;

/// <summary>
/// The EAS projects the Expo token can see; with an app, the best matches (bundle id or package name, then name) first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasProjectListQuery : IQuery<List<EasProjectDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App to suggest projects for.
    /// </summary>
    public int? AppId { get; set; }
}
