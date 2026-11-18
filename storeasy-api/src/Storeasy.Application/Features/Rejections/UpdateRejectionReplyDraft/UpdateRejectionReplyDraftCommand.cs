using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionReplyDraft;

/// <summary>
/// Saves the Resolution Center reply draft (written by a member or by Claude through MCP; never sent by storeasy). A
/// draft over the 4,000 character limit is saved too; the answer says it is over the limit.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateRejectionReplyDraftCommand : ICommand<RejectionReplyDraftDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Case.
    /// </summary>
    public int CaseId { get; set; }

    /// <summary>
    /// Draft text.
    /// </summary>
    public string Body { get; set; }
}
