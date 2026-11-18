using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.ReplyToReview;

/// <summary>
/// Publishes (or replaces) the reply of a review as a store job; returns the job id. Limits: Google Play 350, App Store
/// 5,970 characters. Never sent by Claude on its own: an MCP draft goes through <c>reviews/reply-draft</c>.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record ReplyToReviewCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Review.
    /// </summary>
    public long ReviewId { get; set; }

    /// <summary>
    /// Reply text.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Saved reply the text came from (counted as a use).
    /// </summary>
    public int? SavedReplyId { get; set; }
}
