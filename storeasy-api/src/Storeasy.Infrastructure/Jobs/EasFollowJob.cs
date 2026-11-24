using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every two minutes: follows the EAS builds still running or not created yet (<see cref="IEasService.FollowAsync"/>).
/// Nothing runs while the <c>EasBuilds</c> feature flag is off.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class EasFollowJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<IEasService>().FollowAsync(cancellationToken);
}
