using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.FeedbackDtos;

namespace Storeasy.Application.Features.Reviews.GenerateReviewReplyDraft;

/// <summary>
/// Writes a reply draft for a review with Claude (the organization's Anthropic API key) and saves it as the review's draft,
/// replacing the one there, exactly like <c>reviews/reply-draft</c>. Nothing goes to the store: a member reads, edits and
/// sends the reply with <c>reviews/reply</c>. Needs the same permission as saving a draft.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record GenerateReviewReplyDraftCommand : ICommand<GeneratedReplyDraftDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Review.
    /// </summary>
    public long ReviewId { get; set; }
}
