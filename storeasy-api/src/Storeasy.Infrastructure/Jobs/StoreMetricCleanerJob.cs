using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Deletes the daily store page metric rows older than 25 months (<c>storeMetric_cleanup.sql</c>; the same limit as
/// <c>StoreMetricRules.RetentionMonths</c>).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
public class StoreMetricCleanerJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory) : SqlCleanupJob(scheduleConfig, scopeFactory)
{
    /// <inheritdoc/>
    protected override string SqlFileName => "storeMetric_cleanup.sql";
}
