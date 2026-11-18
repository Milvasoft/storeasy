using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.RejectChangePlan;

/// <summary>
/// Rejects a pending change plan with a reason; nothing is written to the store. Anyone who could approve the plan can reject it.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record RejectChangePlanCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan id.
    /// </summary>
    public long PlanId { get; set; }

    /// <summary>
    /// Why (at least 10 characters).
    /// </summary>
    public string Reason { get; set; }
}
