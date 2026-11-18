using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Events;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every 6 hours: queues the TestFlight syncs (groups, testers, beta review states, build expiry).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class TestFlightSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<ScheduledStoreSyncs>().QueueTestFlightSyncsAsync(cancellationToken);
}
