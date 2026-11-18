using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.ApplyChangePlan;

/// <summary>
/// Approves a pending change plan and queues its apply job (listings/apply, screenshots/apply, previews/apply, releases/apply and plans/approve all send this). The caller needs the plan's own permission (production releases ReleaseProduction, submissions SubmitForReview) and, for those, the typed confirmation. The store is read again first; when the touched values changed the approval is refused (409) and nothing is written.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record ApplyChangePlanCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Plan id.
    /// </summary>
    public long PlanId { get; set; }

    /// <summary>
    /// Typed confirmation (app name and version) for plans that need one.
    /// </summary>
    public string Confirm { get; set; }
}
