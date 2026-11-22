using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Stores.Winget;

/// <summary>
/// Registers the winget channel: the GitHub client, the store channel (gateway calls, sync job, key replacement), the
/// content read, the plan extension that opens pull requests, readiness and the sync job. The pattern every new channel
/// follows (docs/adding-a-store.md).
/// </summary>
public static class WingetServiceCollectionExtensions
{
    /// <summary>
    /// Adds the winget services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddWingetServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(WingetOptions.SectionName).Get<WingetOptions>() ?? new WingetOptions());

        // GitHub calls and installer downloads; tests replace both handlers. Downloads follow redirects by hand.
        services.AddHttpClient(WingetClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient(WingetClient.DownloadClientName, client => client.Timeout = TimeSpan.FromMinutes(30))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        services.AddSingleton<IWingetClient, WingetClient>();
        services.AddSingleton<IStoreChannel, WingetChannel>();
        services.AddScoped<IStoreContentGateway, WingetContentGateway>();
        services.AddScoped<IChangePlanExtension, WingetPlanExtension>();
        services.AddScoped<IReadinessContributor, WingetReadiness>();
        services.AddScoped<IStoreJobHandler, WingetSyncJobHandler>();

        return services;
    }
}
