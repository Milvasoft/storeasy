using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Events;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every 15 minutes: queues the App Store version state syncs (catches rejections a missed webhook did not report).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class VersionStateSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<ScheduledStoreSyncs>().QueueVersionStateSyncsAsync(cancellationToken);
}
