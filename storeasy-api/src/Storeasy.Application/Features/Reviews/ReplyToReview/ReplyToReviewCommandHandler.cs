using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.ReplyToReview;

/// <summary>
/// Handles <see cref="ReplyToReviewCommand"/>. The review shows "sending" until the job publishes the reply.
/// </summary>
/// <param name="ReviewRepository"></param>
/// <param name="SavedReplyRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
[Log]
public record ReplyToReviewCommandHandler(IStoreasyRepositoryBase<StoreReview> ReviewRepository,
                                          IStoreasyRepositoryBase<SavedReply> SavedReplyRepository,
                                          IStoreJobQueue StoreJobQueue,
                                          IAuditTrail AuditTrail,
                                          ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<ReplyToReviewCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = ReviewRepository;
    private readonly IStoreasyRepositoryBase<SavedReply> _savedReplyRepository = SavedReplyRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(ReplyToReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviewRepository.GetFirstOrDefaultAsync(r => r.Id == request.ReviewId && r.OrganizationId == request.OrganizationId,
                                                                    projection: r => new { r.Id, r.Store, r.AppId, r.ReplyState },
                                                                    cancellationToken: cancellationToken);

        if (review == null)
            return Response<long>.Error(0, MessageKey.ReviewNotFound);

        var maxLength = ReviewRules.ReplyMaxLength(review.Store);

        if (maxLength == 0)
            return Response<long>.Error(0, MessageKey.ReviewReplyNotSupported);

        var body = request.Body.Trim();

        if (ReviewRules.Length(body) > maxLength)
            return Response<long>.Error(0, MessageKey.ReviewReplyTooLong);

        var claimed = await _reviewRepository.ExecuteUpdateAsync(r => r.Id == review.Id && r.ReplyState != ReviewReplyState.Sending,
                                                                 new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.ReplyState, ReviewReplyState.Sending)
                                                                                                      .SetPropertyValue(r => r.ReplyError, null)
                                                                                                      .SetPropertyValue(r => r.ReplyByUserName, _currentUser.UserName)
                                                                                                      .SetPropertyValue(r => r.DraftBody, body),
                                                                 cancellationToken);

        if (claimed == 0)
            return Response<long>.Error(0, MessageKey.ReviewReplyInProgress);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.ReplyToReview, new ReplyToReviewPayload(review.Id, body, _currentUser.Id), review.Store, review.AppId)], cancellationToken);

        await _reviewRepository.ExecuteUpdateAsync(review.Id, new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.ReplyJobId, (long?)jobIds[0]), cancellationToken);

        if (request.SavedReplyId.HasValue)
        {
            var useCount = await _savedReplyRepository.GetFirstOrDefaultAsync(s => s.Id == request.SavedReplyId && s.OrganizationId == request.OrganizationId,
                                                                              projection: s => (int?)s.UseCount,
                                                                              cancellationToken: cancellationToken);

            if (useCount.HasValue)
                await _savedReplyRepository.ExecuteUpdateAsync(request.SavedReplyId.Value, new SetPropertyBuilder<SavedReply>().SetPropertyValue(s => s.UseCount, useCount.Value + 1), cancellationToken);
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.ReviewReplied, review.Id.ToString(), $"Reply to review {review.Id} sent ({ReviewRules.Length(body)} characters).", review.Store, cancellationToken: cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
