using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Subscriptions.ApplySubscriptionPlan;

/// <summary>
/// Approves a pending subscription or In-App Purchase plan and queues its apply job; the plan's own permission and typed confirmation are checked.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ApplySubscriptionPlanCommand : ICommand<ChangePlanDto>, IOrganizationRequest
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
