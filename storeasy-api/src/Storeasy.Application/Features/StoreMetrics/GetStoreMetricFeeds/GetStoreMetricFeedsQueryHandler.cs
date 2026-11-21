using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreMetrics.GetStoreMetricFeeds;

/// <summary>
/// Handles <see cref="GetStoreMetricFeedsQuery"/>. A record that was never synced gets the state it would start in. An
/// app of another organization answers 404.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="FeedRepository"></param>
/// <param name="AppRepository"></param>
public record GetStoreMetricFeedsQueryHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                              IStoreasyRepositoryBase<StoreMetricFeed> FeedRepository,
                                              IStoreasyRepositoryBase<App> AppRepository) : IInterceptable, IQueryHandler<GetStoreMetricFeedsQuery, List<StoreMetricFeedDto>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<StoreMetricFeed> _feedRepository = FeedRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;

    /// <inheritdoc/>
    public async Task<Response<List<StoreMetricFeedDto>>> Handle(GetStoreMetricFeedsQuery request, CancellationToken cancellationToken)
    {
        var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                              projection: a => new App { Id = a.Id },
                                                              cancellationToken: cancellationToken);

        if (app == null)
            return Response<List<StoreMetricFeedDto>>.Error(null, MessageKey.AppNotFound);

        var records = await _storeAppRepository.GetAllAsync(s => s.AppId == request.AppId && s.OrganizationId == request.OrganizationId,
                                                            projection: s => new StoreApp
                                                            {
                                                                Id = s.Id,
                                                                Store = s.Store,
                                                                StoreConnectionId = s.StoreConnectionId,
                                                                StoreConnection = s.StoreConnection == null ? null : new StoreConnection { Name = s.StoreConnection.Name, ReportBucket = s.StoreConnection.ReportBucket },
                                                            },
                                                            cancellationToken: cancellationToken);

        var recordIds = records.Select(r => r.Id).ToList();
        var feeds = (await _feedRepository.GetAllAsync(f => f.OrganizationId == request.OrganizationId && recordIds.Contains(f.StoreAppId),
                                                       projection: f => new StoreMetricFeed
                                                       {
                                                           StoreAppId = f.StoreAppId,
                                                           Status = f.Status,
                                                           ExternalRequestId = f.ExternalRequestId,
                                                           EnabledDate = f.EnabledDate,
                                                           EnabledByUserName = f.EnabledByUserName,
                                                           LastFetchedDate = f.LastFetchedDate,
                                                           LastSucceededDate = f.LastSucceededDate,
                                                           EarliestDataDate = f.EarliestDataDate,
                                                           LatestDataDate = f.LatestDataDate,
                                                           LastError = f.LastError,
                                                           LastJobId = f.LastJobId,
                                                       },
                                                       cancellationToken: cancellationToken)).ToDictionary(f => f.StoreAppId);

        var result = records.OrderBy(r => r.Store)
                            .Select(record =>
                            {
                                var feed = feeds.GetValueOrDefault(record.Id);
                                var bucket = record.Store == StoreKind.Google ? record.StoreConnection?.ReportBucket : null;

                                return new StoreMetricFeedDto
                                {
                                    StoreAppId = record.Id,
                                    Store = record.Store,
                                    ConnectionId = record.StoreConnectionId,
                                    ConnectionName = record.StoreConnection?.Name,
                                    Status = feed?.Status ?? InitialStatus(record.Store, bucket),
                                    CanEnable = record.Store == StoreKind.Apple && record.StoreConnectionId != null && string.IsNullOrEmpty(feed?.ExternalRequestId),
                                    ReportBucket = bucket,
                                    EnabledDate = feed?.EnabledDate,
                                    EnabledByUserName = feed?.EnabledByUserName,
                                    LastFetchedDate = feed?.LastFetchedDate,
                                    LastSucceededDate = feed?.LastSucceededDate,
                                    EarliestDataDate = feed?.EarliestDataDate,
                                    LatestDataDate = feed?.LatestDataDate,
                                    LastError = feed?.LastError,
                                    LastJobId = feed?.LastJobId,
                                };
                            })
                            .ToList();

        return Response<List<StoreMetricFeedDto>>.Success(result);
    }

    private static MetricFeedStatus InitialStatus(StoreKind store, string bucket) => store switch
    {
        StoreKind.Apple => MetricFeedStatus.NotEnabled,
        StoreKind.Google when string.IsNullOrEmpty(bucket) => MetricFeedStatus.NeedsSetup,
        _ => MetricFeedStatus.Waiting,
    };
}
