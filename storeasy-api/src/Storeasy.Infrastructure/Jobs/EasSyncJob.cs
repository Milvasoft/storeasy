using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Hourly: queues a sync of the EAS builds for every organization with a linked app and an Expo connection, so builds
/// started elsewhere (eas-cli, GitHub triggers) show up (<see cref="IEasService.QueueLinkedAsync"/>).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class EasSyncJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<IEasService>().QueueLinkedAsync(cancellationToken);
}
