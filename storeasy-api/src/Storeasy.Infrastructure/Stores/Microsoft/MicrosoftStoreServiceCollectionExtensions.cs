using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// Registers the Microsoft Store engine: options, the token cache, the typed client with its named HTTP client, the
/// expiry reader and the store job handlers.
/// </summary>
public static class MicrosoftStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Microsoft Store services (configuration section <see cref="MicrosoftStoreOptions.SectionName"/>).
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddMicrosoftStoreServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(MicrosoftStoreOptions.SectionName).Get<MicrosoftStoreOptions>() ?? new MicrosoftStoreOptions());
        services.AddSingleton<MicrosoftTokenCache>();
        services.AddScoped<IMicrosoftStoreClient, MicrosoftStoreClient>();
        services.AddScoped<IMsConnectionExpiryReader, MsConnectionExpiryReader>();

        // One client for both Microsoft APIs, Entra, blob uploads and downloads; tests replace its handler.
        services.AddHttpClient(MicrosoftStoreClient.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(30));

        services.AddScoped<IStoreJobHandler, MsSyncJobHandler>();
        services.AddScoped<IStoreJobHandler, MsApplyChangeJobHandler>();
        services.AddScoped<IStoreJobHandler, MsSubmitJobHandler>();
        services.AddScoped<IStoreJobHandler, MsCheckInstallerJobHandler>();

        return services;
    }
}
