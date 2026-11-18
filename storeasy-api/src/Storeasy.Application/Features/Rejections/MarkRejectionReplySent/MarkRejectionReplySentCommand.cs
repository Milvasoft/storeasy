using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.MarkRejectionReplySent;

/// <summary>
/// Records that the reply draft was sent in the Resolution Center (by hand or by the assisted step); the App Store
/// Connect API cannot send it. The draft must fit the 4,000 character limit.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record MarkRejectionReplySentCommand : ICommand<RejectionReplyDraftDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }
}
