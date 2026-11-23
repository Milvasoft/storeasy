using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Jobs;
using System.Net;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// Registers the package registries module (docs/package-registries.md): the reader of the registries' public APIs, the
/// syncer, the sync job and the hourly schedule. The module is separate from the stores and is switched off with the
/// <c>Registries</c> feature flag.
/// </summary>
public static class RegistriesServiceCollectionExtensions
{
    /// <summary>
    /// Adds the package registries services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddRegistryServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(RegistryOptions.SectionName).Get<RegistryOptions>() ?? new RegistryOptions());

        // Public, anonymous reads; NuGet's registration answers are gzip. Tests replace the handler.
        services.AddHttpClient(PackageRegistryReader.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(2))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });

        services.AddSingleton<IPackageRegistryReader, PackageRegistryReader>();
        services.AddScoped<IRegistrySyncer, RegistrySyncer>();
        services.AddScoped<IStoreJobHandler, RegistrySyncJobHandler>();
        services.AddScoped<IRegistryReminderChecker, RegistryReminderChecker>();

        services.AddMilvaCronJob<RegistrySyncJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "25 * * * *";
            c.CronFormat = CronFormat.Standard;
        });

        // Token expiry reminders, daily.
        services.AddMilvaCronJob<RegistryReminderJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "0 7 * * *";
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
