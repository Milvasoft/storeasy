using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionText;

/// <summary>
/// Saves the review text pasted from the Resolution Center (or written by the assisted step); the cited guidelines are
/// parsed and become checklist items. When no guideline is found the text is kept and a warning is returned.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateRejectionTextCommand : ICommand<RejectionCaseDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }

    /// <summary>
    /// Review text.
    /// </summary>
    public string RawText { get; set; }
}
