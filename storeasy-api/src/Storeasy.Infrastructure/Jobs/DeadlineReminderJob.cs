using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Daily: TestFlight build expiry reminders (14, 7, 1 days), compliance deadline reminders (30, 7, 1 days) and Microsoft
/// Entra client secret expiry reminders (30, 7, 1 days).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
/// <param name="options"></param>
public class DeadlineReminderJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory, EventScheduleOptions options) : EventScheduleJob(scheduleConfig, scopeFactory, options)
{
    /// <inheritdoc/>
    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var notifier = services.GetRequiredService<IDeadlineNotifier>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;

        return await notifier.NotifyBuildExpiryAsync(now, cancellationToken)
               + await notifier.NotifyComplianceDeadlinesAsync(now, cancellationToken)
               + await notifier.NotifyMsSecretExpiryAsync(now, cancellationToken);
    }
}
