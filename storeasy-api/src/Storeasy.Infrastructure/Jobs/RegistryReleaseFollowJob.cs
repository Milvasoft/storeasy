using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Every five minutes: follows the started releases (<see cref="IRegistryPlanService.FollowAsync"/>): the CI run on GitHub,
/// then the registries until they show the version, or the follow window ends.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class RegistryReleaseFollowJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<IRegistryPlanService>().FollowAsync(cancellationToken);
}
