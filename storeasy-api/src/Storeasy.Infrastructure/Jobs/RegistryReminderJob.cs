using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Daily: warns about registry tokens members recorded (never the tokens) 14 and 3 days before and on their expiry
/// (<see cref="IRegistryReminderChecker"/>). Nothing runs while the module's feature flag is off.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class RegistryReminderJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken) => services.GetRequiredService<IRegistryReminderChecker>().CheckAsync(null, cancellationToken);
}
