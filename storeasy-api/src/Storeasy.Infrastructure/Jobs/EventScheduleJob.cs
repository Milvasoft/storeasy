using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Core.Abstractions;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Base of the scheduled jobs of the events phase: runs <see cref="RunAsync"/> in a fresh scope when
/// <see cref="EventScheduleOptions.SchedulesEnabled"/>, logs and swallows a failure (the next run tries again).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public abstract class EventScheduleJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : MilvaCronJobService(scheduleConfig)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly EventScheduleOptions _options = options;

    /// <summary>
    /// The work of one run; returns the number of items it handled (for the log).
    /// </summary>
    protected abstract Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!_options.SchedulesEnabled)
            return;

        using var scope = _scopeFactory.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<IMilvaLogger>();
        var jobName = GetType().Name;

        try
        {
            var handled = await RunAsync(scope.ServiceProvider, cancellationToken);

            logger.Information(LogTemplate.JobExecuted, jobName, handled);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, LogTemplate.JobException, jobName, ex.Message);
        }
    }
}
