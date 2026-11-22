using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// Registers the Firefox Add-ons channel: the AMO client, the store channel (gateway calls, sync job, key replacement,
/// reviews and replies), the content read, the plan extension, readiness and the sync job (docs/adding-a-store.md).
/// </summary>
public static class FirefoxServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Firefox Add-ons services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddFirefoxServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(FirefoxOptions.SectionName).Get<FirefoxOptions>() ?? new FirefoxOptions());

        // API calls and package uploads (AMO takes packages up to 200 MB). Tests replace the handler.
        services.AddHttpClient(AmoClient.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(15));

        services.AddSingleton<IAmoClient, AmoClient>();
        services.AddSingleton<FirefoxChannel>();
        services.AddSingleton<IStoreChannel>(provider => provider.GetRequiredService<FirefoxChannel>());
        services.AddSingleton<IStoreChannelReviews>(provider => provider.GetRequiredService<FirefoxChannel>());
        services.AddSingleton<IStoreChannelReplies>(provider => provider.GetRequiredService<FirefoxChannel>());
        services.AddScoped<IStoreContentGateway, FirefoxContentGateway>();
        services.AddScoped<IChangePlanExtension, FirefoxPlanExtension>();
        services.AddScoped<IReadinessContributor, FirefoxReadiness>();
        services.AddScoped<IStoreJobHandler, FirefoxSyncJobHandler>();

        return services;
    }
}
