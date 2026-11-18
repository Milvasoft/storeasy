using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Iap.ApplyIapPlan;

/// <summary>
/// Approves a pending In-App Purchase or subscription plan and queues its apply job; the plan's own permission and typed confirmation are checked.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ApplyIapPlanCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan.
    /// </summary>
    public long PlanId { get; set; }

    /// <summary>
    /// Typed confirmation, for plans that need one.
    /// </summary>
    public string Confirm { get; set; }
}
