using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// The <see cref="StoreJobKind.SyncStoreMetrics"/> job: reads the store page metrics of one store record and upserts the
/// daily rows. The first read goes back as far as the source allows (App Store: every report instance since the reports
/// were enabled; Google Play: <see cref="StoreMetricRules.PlayBackfillMonths"/> monthly files; Microsoft Store:
/// <see cref="StoreMetricRules.MicrosoftBackfillDays"/> days); later reads repeat the last
/// <see cref="StoreMetricRules.RefreshDays"/> days, which the stores complete late. Only reads: an App Store record without
/// analytics reports adopts a request made elsewhere or stays "not enabled". A refused credential (401, 403) is saved as
/// <see cref="MetricFeedStatus.AccessDenied"/> and ends the job without retries; other failures are saved and rethrown.
/// </summary>
/// <param name="feeds"></param>
/// <param name="connectionRepository"></param>
/// <param name="gateway"></param>
/// <param name="writer"></param>
/// <param name="timeProvider"></param>
public sealed class SyncStoreMetricsJobHandler(StoreMetricFeeds feeds,
                                               IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                               IStoreMetricsGateway gateway,
                                               StoreMetricWriter writer,
                                               TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly StoreMetricFeeds _feeds = feeds;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreMetricsGateway _gateway = gateway;
    private readonly StoreMetricWriter _writer = writer;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncStoreMetrics;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncStoreMetricsPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);
        var record = await _feeds.RecordAsync(context.OrganizationId, payload.StoreAppId, cancellationToken);
        var feed = await _feeds.LoadAsync(record, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        feed.LastFetchedDate = now;
        feed.LastJobId = context.JobId;

        try
        {
            var connection = await context.OpenConnectionAsync(record.StoreConnectionId.Value, cancellationToken);
            var batch = await ReadAsync(context, record, feed, connection.SecretJson, DateOnly.FromDateTime(now), cancellationToken);

            if (batch == null)
            {
                await _feeds.SaveAsync(feed, cancellationToken);

                return new StoreJobOutcome($"Nothing to read: {feed.Status}.", new { feed.Status });
            }

            var saveStep = await context.BeginStepAsync("metrics.save", StepDirection.Internal, cancellationToken: cancellationToken);
            var written = await _writer.UpsertAsync(record.OrganizationId, record.Id, record.Store, batch.Rows, cancellationToken);

            await context.EndStepAsync(saveStep, StoreJobStepStatus.Succeeded, $"{written} rows written.", cancellationToken);

            if (batch.Rows.Count > 0)
            {
                var first = batch.Rows.Min(r => r.Date);
                var last = batch.Rows.Max(r => r.Date);

                feed.EarliestDataDate = feed.EarliestDataDate.HasValue && feed.EarliestDataDate < first ? feed.EarliestDataDate : first;
                feed.LatestDataDate = feed.LatestDataDate.HasValue && feed.LatestDataDate > last ? feed.LatestDataDate : last;
            }

            feed.Marker = batch.Marker ?? feed.Marker;
            feed.Status = feed.LatestDataDate.HasValue ? MetricFeedStatus.Active : MetricFeedStatus.Waiting;
            feed.EnabledDate ??= now;
            feed.LastSucceededDate = now;
            feed.LastError = null;

            await _feeds.SaveAsync(feed, cancellationToken);

            return new StoreJobOutcome($"{batch.Rows.Count} metric rows read.", new { Rows = batch.Rows.Count, Written = written, feed.LatestDataDate });
        }
        catch (StoreCallException ex) when (ex.StatusCode is 401 or 403)
        {
            feed.Status = MetricFeedStatus.AccessDenied;
            feed.LastError = ex.Message;

            await _feeds.SaveAsync(feed, cancellationToken);

            return new StoreJobOutcome($"The store refused access: {ex.Message}", new { feed.Status });
        }
        catch (Exception ex) when (ex is StoreJobException or FormatException or System.Text.Json.JsonException)
        {
            feed.Status = feed.Status == MetricFeedStatus.Active ? MetricFeedStatus.Active : MetricFeedStatus.Failed;
            feed.LastError = ex.Message;

            await _feeds.SaveAsync(feed, CancellationToken.None);

            if (ex is StoreJobException)
                throw;

            throw new StoreJobException($"A metrics report could not be read: {ex.Message}", transient: false, ex);
        }
    }

    private async Task<StoreMetricBatch> ReadAsync(IStoreJobContext context, StoreApp record, StoreMetricFeed feed, string secretJson, DateOnly today, CancellationToken cancellationToken)
    {
        switch (record.Store)
        {
            case StoreKind.Apple:
            {
                if (string.IsNullOrEmpty(feed.ExternalRequestId))
                {
                    var findStep = await context.BeginStepAsync("metrics.apple.find", StepDirection.Outbound, record.ExternalId, cancellationToken: cancellationToken);
                    var existing = await _gateway.FindAppleReportRequestAsync(secretJson, record.ExternalId, cancellationToken);

                    await context.EndStepAsync(findStep, StoreJobStepStatus.Succeeded, existing == null ? "No report request." : $"Request {existing.Id}.", cancellationToken);

                    if (existing == null || existing.Stopped)
                    {
                        feed.Status = MetricFeedStatus.NotEnabled;
                        feed.LastError = existing?.Stopped == true ? "Apple stopped the analytics reports of this app after a long time without reads. Enable them again." : null;

                        return null;
                    }

                    feed.ExternalRequestId = existing.Id;
                }

                var step = await context.BeginStepAsync("metrics.apple.read", StepDirection.Outbound, record.ExternalId, cancellationToken: cancellationToken);
                var batch = await _gateway.ReadAppleMetricsAsync(secretJson, feed.ExternalRequestId, feed.Marker?.AddDays(-StoreMetricRules.RefreshDays), cancellationToken);

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, batch.ReportsReady ? $"{batch.Rows.Count} rows." : "Apple has not generated the reports yet.", cancellationToken);

                return batch;
            }
            case StoreKind.Google:
            {
                var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == record.StoreConnectionId,
                                                                                    projection: c => new StoreConnection { Id = c.Id, ReportBucket = c.ReportBucket },
                                                                                    cancellationToken: cancellationToken);

                if (string.IsNullOrEmpty(connection?.ReportBucket))
                {
                    feed.Status = MetricFeedStatus.NeedsSetup;
                    feed.LastError = null;

                    return null;
                }

                var from = feed.LatestDataDate?.AddDays(-StoreMetricRules.RefreshDays) ?? new DateOnly(today.Year, today.Month, 1).AddMonths(-(StoreMetricRules.PlayBackfillMonths - 1));
                var step = await context.BeginStepAsync("metrics.play.read", StepDirection.Outbound, record.ExternalId, cancellationToken: cancellationToken);
                var batch = await _gateway.ReadPlayMetricsAsync(secretJson, connection.ReportBucket, record.ExternalId, from, today, cancellationToken);

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{batch.Rows.Count} rows.", cancellationToken);

                return batch;
            }
            case StoreKind.Microsoft:
            {
                var to = today.AddDays(-1);
                var from = feed.LatestDataDate?.AddDays(-StoreMetricRules.RefreshDays) ?? today.AddDays(-StoreMetricRules.MicrosoftBackfillDays);
                var step = await context.BeginStepAsync("metrics.microsoft.read", StepDirection.Outbound, record.ExternalId, cancellationToken: cancellationToken);
                var batch = await _gateway.ReadMicrosoftMetricsAsync(secretJson, record.ExternalId, from > to ? to : from, to, cancellationToken);

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{batch.Rows.Count} rows.", cancellationToken);

                return batch;
            }
            default:
                throw new StoreJobException($"{record.Store} has no metrics.", transient: false);
        }
    }
}
