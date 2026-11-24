using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Jobs;

namespace Storeasy.Infrastructure.Eas;

/// <summary>
/// Registers EAS builds (docs/eas-builds.md): the Expo client, the service, the sync job and the schedules. Switched off
/// with the <c>EasBuilds</c> feature flag.
/// </summary>
public static class EasServiceCollectionExtensions
{
    /// <summary>
    /// Adds the EAS services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddEasServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(EasOptions.SectionName).Get<EasOptions>() ?? new EasOptions());

        // Tests replace the handler with a fake Expo.
        services.AddHttpClient(ExpoClient.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(2));
        services.AddSingleton<IExpoApi, ExpoClient>();
        services.AddScoped<IEasService, EasService>();
        services.AddScoped<IStoreJobHandler, EasSyncJobHandler>();

        services.AddMilvaCronJob<EasFollowJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "*/2 * * * *";
            c.CronFormat = CronFormat.Standard;
        });

        services.AddMilvaCronJob<EasSyncJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "40 * * * *";
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
