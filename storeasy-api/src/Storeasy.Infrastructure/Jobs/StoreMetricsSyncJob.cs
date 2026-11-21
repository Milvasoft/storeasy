using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Events;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every day at 05:30 UTC: queues the store page metrics syncs (the stores publish the previous day's reports during the
/// night, US time).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class StoreMetricsSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<ScheduledStoreSyncs>().QueueStoreMetricSyncsAsync(cancellationToken);
}
