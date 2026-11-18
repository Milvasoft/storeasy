using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.DiscardChangePlan;

/// <summary>
/// Withdraws a pending change plan; nothing is written to the store.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record DiscardChangePlanCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan id.
    /// </summary>
    public long PlanId { get; set; }
}
