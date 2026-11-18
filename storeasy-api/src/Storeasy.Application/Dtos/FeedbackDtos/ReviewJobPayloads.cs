namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncReviews"/> job.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
public sealed record SyncReviewsPayload(int StoreAppId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.ReplyToReview"/> job.
/// </summary>
/// <param name="ReviewId">Review.</param>
/// <param name="Body">Reply text.</param>
/// <param name="RequestedByUserId">Member who sent it (notified on failure).</param>
public sealed record ReplyToReviewPayload(long ReviewId, string Body, int? RequestedByUserId);
