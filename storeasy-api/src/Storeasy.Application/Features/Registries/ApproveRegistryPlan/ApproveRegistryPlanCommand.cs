using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.ApproveRegistryPlan;

/// <summary>
/// Approves a pending release or operation plan: the member needs the plan's permission (production release) and types its
/// confirmation text; a version bump whose files changed since the plan is refused. The write then runs in a job.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record ApproveRegistryPlanCommand : ICommand<RegistryPlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan.
    /// </summary>
    public long PlanId { get; set; }

    /// <summary>
    /// The plan's confirmation text, typed.
    /// </summary>
    public string Confirm { get; set; }
}
