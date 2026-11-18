using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.SaveReviewReplyDraft;

/// <summary>
/// Saves an unsent reply draft (a member's, or Claude's through MCP). Nothing goes to the store.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record SaveReviewReplyDraftCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Review.
    /// </summary>
    public long ReviewId { get; set; }

    /// <summary>
    /// Draft text (empty clears it).
    /// </summary>
    public string Body { get; set; }
}
