using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Infrastructure.Jobs;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// Registrations of the store page metrics (ASO conversion analytics): the store reads, the daily rows, the query service
/// other features reuse and the daily sync.
/// </summary>
public static class StoreMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the store page metrics services. The store HTTP client (<see cref="StoreClients.StoreApiClient"/>) comes from
    /// the events phase and the Microsoft Store client from the Microsoft phase.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddStoreMetricServices(this IServiceCollection services)
    {
        services.AddScoped<IStoreMetricsGateway, StoreMetricsGateway>();
        services.AddScoped<IStoreMetricsQuery, StoreMetricsQuery>();
        services.AddScoped<StoreMetricWriter>();
        services.AddScoped<StoreMetricFeeds>();

        services.AddScoped<IStoreJobHandler, SyncStoreMetricsJobHandler>();
        services.AddScoped<IStoreJobHandler, EnableAppleAnalyticsJobHandler>();

        // Every day at 05:30 UTC (only while Storeasy:Events:SchedulesEnabled is on, like the other store syncs).
        services.AddMilvaCronJob<StoreMetricsSyncJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "30 5 * * *";
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
