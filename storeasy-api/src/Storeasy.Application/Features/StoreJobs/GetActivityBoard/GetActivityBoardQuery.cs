using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.StoreJobs.GetActivityBoard;

/// <summary>
/// The live activity page's board: versions with App Review, builds processing or close to expiry, and the last 24 hours
/// of finished jobs and store events per store. Read from storeasy's own records; no store is called.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetActivityBoardQuery : IQuery<ActivityBoardDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
