using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Feedback;
using System.Globalization;

namespace Storeasy.Infrastructure.Feedback;

/// <summary>
/// The <see cref="StoreJobKind.ReplyToReview"/> job: publishes the reply and records the outcome on the review. A failed
/// attempt shows "failed" with the store's reason; a permanent failure tells the member who sent it.
/// </summary>
/// <param name="reviewRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="orgWebhookPublisher"></param>
/// <param name="timeProvider"></param>
public sealed class ReplyToReviewJobHandler(IStoreasyRepositoryBase<StoreReview> reviewRepository,
                                           IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                           IStoreReviewGateway gateway,
                                           INotificationDispatcher notificationDispatcher,
                                           IOrgWebhookPublisher orgWebhookPublisher,
                                           TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = reviewRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreReviewGateway _gateway = gateway;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly IOrgWebhookPublisher _orgWebhookPublisher = orgWebhookPublisher;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.ReplyToReview;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<ReplyToReviewPayload>() ?? throw new StoreJobException("The job has no review.", transient: false);

        var review = await _reviewRepository.GetFirstOrDefaultAsync(r => r.Id == payload.ReviewId && r.OrganizationId == context.OrganizationId,
                                                                    projection: r => new StoreReview { Id = r.Id, AppId = r.AppId, StoreAppId = r.StoreAppId, Store = r.Store, ExternalReviewId = r.ExternalReviewId, ReplyExternalId = r.ReplyExternalId },
                                                                    cancellationToken: cancellationToken) ?? throw new StoreJobException("The review was removed.", transient: false);

        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == review.StoreAppId,
                                                                        projection: s => new StoreApp { Id = s.Id, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId, Name = s.App.Name },
                                                                        cancellationToken: cancellationToken);

        var connection = storeApp?.StoreConnectionId != null ? await context.OpenConnectionAsync(storeApp.StoreConnectionId.Value, cancellationToken) : null;

        await SetStateAsync(review.Id, new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.ReplyState, ReviewReplyState.Sending).SetPropertyValue(r => r.ReplyError, null), cancellationToken);

        try
        {
            if (connection == null)
                throw new StoreJobException("The store record has no connection.", transient: false);

            var step = await context.BeginStepAsync("review.reply", StepDirection.Outbound, review.ExternalReviewId, cancellationToken: cancellationToken);
            var reply = await _gateway.ReplyAsync(review.Store, connection.SecretJson, storeApp.ExternalId, review.ExternalReviewId, review.ReplyExternalId, payload.Body, cancellationToken);

            await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, cancellationToken: cancellationToken);

            await SetStateAsync(review.Id,
                                new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.ReplyState, ReviewReplyState.Published)
                                                                     .SetPropertyValue(r => r.ReplyBody, payload.Body)
                                                                     .SetPropertyValue(r => r.ReplyExternalId, reply.ExternalId)
                                                                     .SetPropertyValue(r => r.ReplyDate, (DateTime?)reply.Date)
                                                                     .SetPropertyValue(r => r.DraftBody, null),
                                cancellationToken);

            await _orgWebhookPublisher.EnqueueAsync(context.OrganizationId, OrgWebhookEventTypes.ReviewReplied, new { reviewId = review.Id, review.AppId, store = review.Store.ToString(), replyDate = reply.Date }, cancellationToken);

            return new StoreJobOutcome($"Reply to review {review.Id} published.", new { ReviewId = review.Id });
        }
        catch (StoreJobException ex)
        {
            await SetStateAsync(review.Id,
                                new SetPropertyBuilder<StoreReview>().SetPropertyValue(r => r.ReplyState, ReviewReplyState.Failed)
                                                                     .SetPropertyValue(r => r.ReplyError, ex.Message?.Length > 500 ? ex.Message[..500] : ex.Message),
                                cancellationToken);

            if (!ex.Transient && payload.RequestedByUserId.HasValue)
            {
                await _notificationDispatcher.DispatchAsync(new OrgNotification
                {
                    OrganizationId = context.OrganizationId,
                    Kind = NotificationType.ReviewReplyFailed,
                    UserIds = [payload.RequestedByUserId.Value],
                    TextKey = MessageKey.NotificationReviewReplyFailed,
                    TextArgs = [storeApp?.Name ?? string.Empty, ex.Message],
                    Data = new { reviewId = review.Id, jobId = context.JobId },
                    AppId = review.AppId,
                    RelatedEntity = NotificationEntity.Review,
                    RelatedEntityId = review.Id.ToString(CultureInfo.InvariantCulture),
                    ActionLink = $"/reviews/{review.Id}",
                }, cancellationToken);
            }

            throw;
        }
    }

    private Task SetStateAsync(long reviewId, SetPropertyBuilder<StoreReview> update, CancellationToken cancellationToken)
        => _reviewRepository.ExecuteUpdateAsync(reviewId, update, cancellationToken);
}
