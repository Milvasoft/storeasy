using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasSubmissionList;

/// <summary>
/// The EAS Submit submissions of an app, newest first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasSubmissionListQuery : IQuery<List<EasSubmissionDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }
}
