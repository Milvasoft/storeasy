using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;

/// <summary>
/// Reads one change plan with its diff, remarks, store calls and state (a plan of another organization is not found).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetChangePlanDetailQuery : IQuery<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan id.
    /// </summary>
    public long PlanId { get; set; }
}
