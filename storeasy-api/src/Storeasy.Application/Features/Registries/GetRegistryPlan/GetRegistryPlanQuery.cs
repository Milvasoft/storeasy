using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetRegistryPlan;

/// <summary>
/// One release or operation plan with its changes and, once started, the CI run and follow state.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRegistryPlanQuery : IQuery<RegistryPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan.
    /// </summary>
    public long PlanId { get; set; }
}
