using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Daily: reads the watched official sources and opens catalog proposals for the changes.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
/// <param name="watcherOptions"></param>
public class RuleWatcherJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options, RuleWatcherOptions watcherOptions) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    private readonly RuleWatcherOptions _watcherOptions = watcherOptions;

    /// <inheritdoc/>
    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!_watcherOptions.Enabled)
            return 0;

        var result = await services.GetRequiredService<IRuleWatcher>().RunAsync(cancellationToken);

        return result.Checked;
    }
}
