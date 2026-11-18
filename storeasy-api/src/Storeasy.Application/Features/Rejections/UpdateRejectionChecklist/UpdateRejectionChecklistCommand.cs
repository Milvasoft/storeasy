using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionChecklist;

/// <summary>
/// Replaces the checklist of a case (items added by hand, statuses, assignees). An item without an id is new.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateRejectionChecklistCommand : ICommand<RejectionCaseDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }

    /// <summary>
    /// The whole checklist.
    /// </summary>
    public List<RejectionChecklistItem> Items { get; set; } = [];
}
