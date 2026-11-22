using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Feedback;

namespace Storeasy.Infrastructure.Feedback;

/// <summary>
/// The <see cref="StoreJobKind.SyncReviews"/> job: reads the recent reviews of one store record and keeps every one (Play
/// returns only recent reviews with text, so older ones stay in storeasy). New reviews are announced once per run, except
/// on the first import of a record.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="reviewRepository"></param>
/// <param name="gateway"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="timeProvider"></param>
/// <param name="channels">Channels that read their own reviews (<see cref="IStoreChannelReviews"/>), asked before the gateway.</param>
public sealed class SyncReviewsJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                          IStoreasyRepositoryBase<StoreReview> reviewRepository,
                                          IStoreReviewGateway gateway,
                                          INotificationDispatcher notificationDispatcher,
                                          TimeProvider timeProvider,
                                          IEnumerable<IStoreChannelReviews> channels = null) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<StoreReview> _reviewRepository = reviewRepository;
    private readonly IStoreReviewGateway _gateway = gateway;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IReadOnlyList<IStoreChannelReviews> _channels = [.. channels ?? []];

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncReviews;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncReviewsPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);

        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == payload.StoreAppId && s.OrganizationId == context.OrganizationId,
                                                                        projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, Store = s.Store, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId, Name = s.App.Name },
                                                                        cancellationToken: cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        var connection = storeApp.StoreConnectionId.HasValue ? await context.OpenConnectionAsync(storeApp.StoreConnectionId.Value, cancellationToken) : null;

        if (connection == null)
            throw new StoreJobException("The store record has no connection.", transient: false);

        var step = await context.BeginStepAsync("reviews.list", StepDirection.Outbound, storeApp.ExternalId, cancellationToken: cancellationToken);
        var channel = _channels.LastOrDefault(c => c.Store == storeApp.Store);
        var records = channel != null
            ? await channel.ListReviewsAsync(connection.SecretJson, storeApp.ExternalId, cancellationToken)
            : await _gateway.ListReviewsAsync(storeApp.Store, connection.SecretJson, storeApp.ExternalId, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{records.Count} reviews.", cancellationToken);

        var saveStep = await context.BeginStepAsync("reviews.save", StepDirection.Internal, cancellationToken: cancellationToken);

        var existing = (await _reviewRepository.GetAllAsync(r => r.StoreAppId == storeApp.Id,
                                                            projection: r => new StoreReview { Id = r.Id, ExternalReviewId = r.ExternalReviewId, ReviewDate = r.ReviewDate, Body = r.Body, Title = r.Title, Rating = r.Rating, AppVersion = r.AppVersion, ReplyBody = r.ReplyBody, ReplyState = r.ReplyState, ReplyExternalId = r.ReplyExternalId, ReplyDate = r.ReplyDate },
                                                            cancellationToken: cancellationToken)).ToDictionary(r => r.ExternalReviewId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var added = new List<StoreReview>();
        var updated = new List<StoreReview>();

        foreach (var record in records.Where(r => !string.IsNullOrEmpty(r.ExternalId)).DistinctBy(r => r.ExternalId))
        {
            var row = existing.GetValueOrDefault(record.ExternalId);
            var isNew = row == null;

            row ??= new StoreReview { OrganizationId = context.OrganizationId, AppId = storeApp.AppId, StoreAppId = storeApp.Id, Store = storeApp.Store, ExternalReviewId = Cut(record.ExternalId, StoreReview.TextMaxLength), FetchedDate = now };

            var changed = isNew || row.ReviewDate != record.Date || row.Body != record.Body || row.Rating != record.Rating;

            row.AuthorName = Cut(record.AuthorName, StoreReview.TextMaxLength);
            row.Rating = record.Rating;
            row.Title = Cut(record.Title, StoreReview.TextMaxLength);
            row.Body = Cut(record.Body, StoreReview.BodyMaxLength);
            row.Locale = Cut(record.Locale, 20);
            row.Territory = Cut(record.Territory, 20);
            row.AppVersion = Cut(record.AppVersion, 50);
            row.Device = Cut(record.Device, StoreReview.TextMaxLength);
            row.ReviewDate = record.Date;

            if (record.ReplyBody != null && row.ReplyState != ReviewReplyState.Sending && (row.ReplyBody != record.ReplyBody || row.ReplyState != ReviewReplyState.Published))
            {
                row.ReplyBody = Cut(record.ReplyBody, StoreReview.BodyMaxLength);
                row.ReplyExternalId = Cut(record.ReplyExternalId, StoreReview.TextMaxLength);
                row.ReplyDate = record.ReplyDate;
                row.ReplyState = ReviewReplyState.Published;
                changed = true;
            }

            if (isNew)
                added.Add(row);
            else if (changed)
                updated.Add(row);
        }

        if (added.Count > 0)
            await _reviewRepository.AddRangeAsync(added, cancellationToken);

        if (updated.Count > 0)
            await _reviewRepository.UpdateAsync(updated, cancellationToken, r => r.AuthorName, r => r.Rating, r => r.Title, r => r.Body, r => r.Locale, r => r.Territory, r => r.AppVersion, r => r.Device, r => r.ReviewDate, r => r.ReplyBody, r => r.ReplyExternalId, r => r.ReplyDate, r => r.ReplyState);

        await context.EndStepAsync(saveStep, StoreJobStepStatus.Succeeded, $"{added.Count} new, {updated.Count} updated.", cancellationToken);

        if (added.Count > 0 && existing.Count > 0)
        {
            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                OrganizationId = context.OrganizationId,
                Kind = NotificationType.NewReview,
                Roles = NotificationAudiences.Editors,
                TextKey = MessageKey.NotificationNewReview,
                TextArgs = [storeApp.Name, added.Count, added.Min(r => r.Rating)],
                Data = new { appId = storeApp.AppId, storeAppId = storeApp.Id, store = storeApp.Store.ToString(), count = added.Count },
                WebhookData = new { appId = storeApp.AppId, storeAppId = storeApp.Id, store = storeApp.Store.ToString(), reviews = added.Take(50).Select(r => new { r.Id, r.Rating, r.Title, r.Body, r.Locale, r.ReviewDate }) },
                AppId = storeApp.AppId,
                RelatedEntity = NotificationEntity.App,
                RelatedEntityId = storeApp.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActionLink = $"/reviews?app={storeApp.AppId}",
                WebhookEventType = OrgWebhookEventTypes.ReviewReceived,
                ActivityType = ActivityEventTypes.ReviewReceived,
            }, cancellationToken);
        }

        return new StoreJobOutcome($"{records.Count} reviews read; {added.Count} new.", new { Read = records.Count, New = added.Count, Updated = updated.Count });
    }

    private static string Cut(string value, int length) => value?.Length > length ? value[..length] : value;
}
