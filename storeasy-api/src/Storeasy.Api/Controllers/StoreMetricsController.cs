using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Features.StoreMetrics.EnableAppleAnalytics;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetricFeeds;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetrics;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetricsSummary;
using Storeasy.Application.Features.StoreMetrics.SetReportBucket;
using Storeasy.Application.Features.StoreMetrics.SyncStoreMetrics;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Store page performance (ASO conversion analytics): impressions, page views, downloads and conversion rate per day,
/// read daily from the App Store Connect analytics reports, the Google Play statistics bucket and the Microsoft Store
/// analytics API. Reads come from storeasy's rows; the only store write is enabling the App Store reports.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/store-metrics")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class StoreMetricsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Metrics of one app over a date range: totals and change against the previous period, daily series, breakdowns by
    /// source, country and store.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<StoreMetricsReportDto>> GetStoreMetricsAsync([FromQuery] GetStoreMetricsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Metrics of every app of the organization over a date range.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("summary")]
    public Task<Response<StoreMetricsSummaryDto>> GetSummaryAsync([FromQuery] GetStoreMetricsSummaryQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The metrics feed of every store record of an app (state, last read, what to set up).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("feeds")]
    public Task<Response<List<StoreMetricFeedDto>>> GetFeedsAsync([FromQuery] GetStoreMetricFeedsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the metrics now (one job per store record). Returns the job ids.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(30, 3600, "store-metrics-sync")]
    public Task<Response<List<long>>> SyncAsync(SyncStoreMetricsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Asks App Store Connect to start the analytics reports of an App Store record (needs <c>Confirm: true</c>). Returns
    /// the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apple/enable")]
    [RateLimit(20, 3600, "store-metrics-apple-enable")]
    public Task<Response<long>> EnableAppleAnalyticsAsync(EnableAppleAnalyticsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sets or clears the reports bucket of a Google Play connection. Returns the stored bucket name.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("google/bucket")]
    public Task<Response<string>> SetReportBucketAsync(SetReportBucketCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
