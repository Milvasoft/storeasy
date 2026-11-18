using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.StoreJobDtos;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobDetail;

/// <summary>
/// Reads one store job of the organization with its steps.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetStoreJobDetailQuery : IQuery<StoreJobDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Job id.
    /// </summary>
    public long JobId { get; set; }
}
