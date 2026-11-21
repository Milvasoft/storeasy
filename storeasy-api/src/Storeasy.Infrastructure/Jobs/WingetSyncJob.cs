using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Events;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every 15 minutes: queues the winget syncs of connections with an open pull request (review state only).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class WingetSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<ScheduledStoreSyncs>().QueueWingetSyncsAsync(cancellationToken);
}
