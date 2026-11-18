using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;

namespace Storeasy.Infrastructure.Jobs;

/// <summary>
/// Deletes the audit log entries older than their organization's retention (<c>Organizations.AuditRetentionDays</c>,
/// <c>auditLog_cleanup.sql</c>).
/// </summary>
/// <param name="scheduleConfig"></param>
/// <param name="scopeFactory"></param>
public class AuditLogCleanerJob(IScheduleConfig scheduleConfig, IServiceScopeFactory scopeFactory) : SqlCleanupJob(scheduleConfig, scopeFactory)
{
    /// <inheritdoc/>
    protected override string SqlFileName => "auditLog_cleanup.sql";
}
