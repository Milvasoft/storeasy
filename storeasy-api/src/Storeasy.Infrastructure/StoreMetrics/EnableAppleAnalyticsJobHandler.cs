using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// The <see cref="StoreJobKind.EnableAppleAnalytics"/> job: asks App Store Connect for the ONGOING analytics reports of
/// one app (or adopts the request that already exists), saves the request id and queues a first metrics sync. Apple needs
/// one or two days for the first reports, so the feed waits until then. A refused key (the step needs the Admin role) is
/// saved as <see cref="MetricFeedStatus.AccessDenied"/> and fails the job.
/// </summary>
/// <param name="feeds"></param>
/// <param name="gateway"></param>
/// <param name="storeJobQueue"></param>
/// <param name="timeProvider"></param>
public sealed class EnableAppleAnalyticsJobHandler(StoreMetricFeeds feeds,
                                                   IStoreMetricsGateway gateway,
                                                   IStoreJobQueue storeJobQueue,
                                                   TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly StoreMetricFeeds _feeds = feeds;
    private readonly IStoreMetricsGateway _gateway = gateway;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.EnableAppleAnalytics;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<EnableAppleAnalyticsPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);
        var record = await _feeds.RecordAsync(context.OrganizationId, payload.StoreAppId, cancellationToken);

        if (record.Store != StoreKind.Apple)
            throw new StoreJobException("Analytics reports are enabled for App Store records only.", transient: false);

        var feed = await _feeds.LoadAsync(record, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var connection = await context.OpenConnectionAsync(record.StoreConnectionId.Value, cancellationToken);
        var step = await context.BeginStepAsync("metrics.apple.enable", StepDirection.Outbound, record.ExternalId, cancellationToken: cancellationToken);

        AppleReportRequest request;

        try
        {
            request = await _gateway.CreateAppleReportRequestAsync(connection.SecretJson, record.ExternalId, cancellationToken);
        }
        catch (StoreCallException ex) when (ex.StatusCode is 401 or 403)
        {
            feed.Status = MetricFeedStatus.AccessDenied;
            feed.LastError = ex.Message;
            feed.LastFetchedDate = now;

            await _feeds.SaveAsync(feed, cancellationToken);
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, ex.Message, cancellationToken);

            throw new StoreJobException($"App Store Connect refused the analytics report request (the API key needs the Admin role): {ex.Message}", transient: false, ex);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"Request {request.Id}.", cancellationToken);

        feed.ExternalRequestId = request.Id;
        feed.Status = feed.LatestDataDate.HasValue ? MetricFeedStatus.Active : MetricFeedStatus.Waiting;
        feed.EnabledDate = now;
        feed.EnabledByUserName = payload.UserName;
        feed.LastError = null;
        feed.LastJobId = context.JobId;

        await _feeds.SaveAsync(feed, cancellationToken);

        await _storeJobQueue.EnqueueAsync([new StoreJobRequest(context.OrganizationId, StoreJobKind.SyncStoreMetrics, new SyncStoreMetricsPayload(record.Id), StoreKind.Apple, record.AppId)], cancellationToken);

        return new StoreJobOutcome($"Analytics reports enabled (request {request.Id}). Apple delivers the first data in one or two days.", new { RequestId = request.Id });
    }
}
