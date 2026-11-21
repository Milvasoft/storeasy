using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// Loads and saves the <see cref="StoreMetricFeed"/> row of a store record for the metrics jobs.
/// </summary>
/// <param name="feedRepository"></param>
/// <param name="storeAppRepository"></param>
public sealed class StoreMetricFeeds(IStoreasyRepositoryBase<StoreMetricFeed> feedRepository, IStoreasyRepositoryBase<StoreApp> storeAppRepository)
{
    private readonly IStoreasyRepositoryBase<StoreMetricFeed> _feedRepository = feedRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;

    /// <summary>
    /// The store record of a job (id, app, store, external id, connection), or a permanent job failure when it is gone.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<StoreApp> RecordAsync(int organizationId, int storeAppId, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == storeAppId && s.OrganizationId == organizationId,
                                                                     projection: s => new StoreApp
                                                                     {
                                                                         Id = s.Id,
                                                                         OrganizationId = s.OrganizationId,
                                                                         AppId = s.AppId,
                                                                         Store = s.Store,
                                                                         ExternalId = s.ExternalId,
                                                                         StoreConnectionId = s.StoreConnectionId,
                                                                     },
                                                                     cancellationToken: cancellationToken) ?? throw new StoreJobException("The store record was removed.", transient: false);

        return record.StoreConnectionId.HasValue ? record : throw new StoreJobException("The store record has no connection.", transient: false);
    }

    /// <summary>
    /// The feed of a record, or a new unsaved one.
    /// </summary>
    /// <param name="record"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<StoreMetricFeed> LoadAsync(StoreApp record, CancellationToken cancellationToken)
        => await _feedRepository.GetFirstOrDefaultAsync(f => f.StoreAppId == record.Id,
                                                        projection: f => new StoreMetricFeed
                                                        {
                                                            Id = f.Id,
                                                            OrganizationId = f.OrganizationId,
                                                            StoreAppId = f.StoreAppId,
                                                            Store = f.Store,
                                                            Status = f.Status,
                                                            ExternalRequestId = f.ExternalRequestId,
                                                            EnabledDate = f.EnabledDate,
                                                            EnabledByUserName = f.EnabledByUserName,
                                                            LastFetchedDate = f.LastFetchedDate,
                                                            LastSucceededDate = f.LastSucceededDate,
                                                            EarliestDataDate = f.EarliestDataDate,
                                                            LatestDataDate = f.LatestDataDate,
                                                            Marker = f.Marker,
                                                            LastError = f.LastError,
                                                            LastJobId = f.LastJobId,
                                                        },
                                                        cancellationToken: cancellationToken)
           ?? new StoreMetricFeed { OrganizationId = record.OrganizationId, StoreAppId = record.Id, Store = record.Store, Status = MetricFeedStatus.NotEnabled };

    /// <summary>
    /// Inserts a new feed or writes every field of an existing one. The error is redacted and cut to the column.
    /// </summary>
    /// <param name="feed"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveAsync(StoreMetricFeed feed, CancellationToken cancellationToken)
    {
        feed.LastError = feed.LastError == null ? null : SecretRedactor.RedactAndTrim(feed.LastError, StoreMetricFeed.ErrorMaxLength);

        if (feed.Id == 0)
        {
            await _feedRepository.AddAsync(feed, cancellationToken);

            return;
        }

        var builder = new SetPropertyBuilder<StoreMetricFeed>().SetPropertyValue(f => f.Status, feed.Status)
                                                               .SetPropertyValue(f => f.ExternalRequestId, feed.ExternalRequestId)
                                                               .SetPropertyValue(f => f.EnabledDate, feed.EnabledDate)
                                                               .SetPropertyValue(f => f.EnabledByUserName, feed.EnabledByUserName)
                                                               .SetPropertyValue(f => f.LastFetchedDate, feed.LastFetchedDate)
                                                               .SetPropertyValue(f => f.LastSucceededDate, feed.LastSucceededDate)
                                                               .SetPropertyValue(f => f.EarliestDataDate, feed.EarliestDataDate)
                                                               .SetPropertyValue(f => f.LatestDataDate, feed.LatestDataDate)
                                                               .SetPropertyValue(f => f.Marker, feed.Marker)
                                                               .SetPropertyValue(f => f.LastError, feed.LastError)
                                                               .SetPropertyValue(f => f.LastJobId, feed.LastJobId);

        await _feedRepository.ExecuteUpdateAsync(feed.Id, builder, cancellationToken);
    }
}
