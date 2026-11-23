using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.RejectRegistryPlan;

/// <summary>
/// Rejects a pending release or operation plan (needs the plan's permission).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record RejectRegistryPlanCommand : ICommand<RegistryPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan.
    /// </summary>
    public long PlanId { get; set; }
}
