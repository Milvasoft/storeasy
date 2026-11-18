using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobList;

/// <summary>
/// Pages through an organization's store jobs, newest first. The standard list request filters apply to the DTO fields.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreJobListQuery : ListRequest, IListRequestQuery<StoreJobListDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Only the jobs of this storeasy app (optional).
    /// </summary>
    public int? AppId { get; set; }
}
