using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// Registers the Chrome Web Store channel: the API client, the store channel (gateway calls, sync job, key replacement,
/// reviews, metrics), the content read, the plan extension, readiness, artifact delivery and the sync job
/// (docs/adding-a-store.md).
/// </summary>
public static class ChromeServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Chrome Web Store services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddChromeServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(ChromeOptions.SectionName).Get<ChromeOptions>() ?? new ChromeOptions());

        // Token and API calls; package uploads up to the store's 2 GB limit take their time. Tests replace the handler.
        services.AddHttpClient(ChromeWebStoreClient.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(30));

        services.AddSingleton<IChromeWebStoreClient, ChromeWebStoreClient>();
        services.AddSingleton<ChromeChannel>();
        services.AddSingleton<IStoreChannel>(provider => provider.GetRequiredService<ChromeChannel>());
        services.AddSingleton<IStoreChannelReviews>(provider => provider.GetRequiredService<ChromeChannel>());
        services.AddSingleton<IStoreChannelMetrics>(provider => provider.GetRequiredService<ChromeChannel>());
        services.AddScoped<IStoreContentGateway, ChromeContentGateway>();
        services.AddScoped<IChangePlanExtension, ChromePlanExtension>();
        services.AddScoped<IReadinessContributor, ChromeReadiness>();
        services.AddScoped<IArtifactChannel, ChromeArtifactChannel>();
        services.AddScoped<IStoreJobHandler, ChromeSyncJobHandler>();

        return services;
    }
}
