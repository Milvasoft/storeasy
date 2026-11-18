using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Core.Abstractions;
using Milvasoft.JobScheduling;
using Storeasy.Application.Utils.Constants;
using Storeasy.Infrastructure.Intake;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Runs <see cref="IntakeCleaner"/> (package retention, abandoned uploads, pending assets, expired upload tokens). Logs and
/// swallows its errors like the other cleanup jobs.
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
public class IntakeCleanupJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory) : MilvaCronJobService(scheduleConfig)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    /// <inheritdoc/>
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<IMilvaLogger>();

        try
        {
            var changed = await scope.ServiceProvider.GetRequiredService<IntakeCleaner>().RunAsync(cancellationToken);

            logger.Information(LogTemplate.JobExecuted, nameof(IntakeCleanupJob), changed);
        }
        catch (Exception ex)
        {
            logger.Error(ex, LogTemplate.JobException, nameof(IntakeCleanupJob), ex.Message);
        }
    }
}
