using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Intake;
using Storeasy.Infrastructure.Intake.Packages;
using Storeasy.Infrastructure.Jobs;
using Storeasy.Infrastructure.Services;
using Storeasy.Infrastructure.Storage;

namespace Storeasy.Infrastructure;

/// <summary>
/// Registrations of the storeasy tokens (personal access, CI, upload) and the package intake (docs/urun-plani.md 11.8).
/// </summary>
public static class IntakeServiceCollectionExtensions
{
    /// <summary>
    /// Adds the token authentication services, the OIDC exchange, the intake service, the processing job, the package
    /// readers, the download client and the intake cleanup job.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <param name="storageOptions"></param>
    /// <returns></returns>
    public static IServiceCollection AddIntakeServices(this IServiceCollection services, IConfigurationManager configurationManager, StorageOptions storageOptions)
    {
        var intakeOptions = configurationManager.GetSection(IntakeOptions.SectionName).Get<IntakeOptions>() ?? new IntakeOptions();

        services.AddSingleton(intakeOptions);

        services.AddScoped<ICurrentToken, CurrentToken>();
        services.AddScoped<IApiTokenAuthenticator, ApiTokenAuthenticator>();

        // OIDC discovery documents and key sets of the allow-listed issuers; the validator caches the keys.
        services.AddHttpClient(OidcTokenValidator.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<IOidcTokenValidator, OidcTokenValidator>();
        services.AddScoped<IOidcExchangeService, OidcExchangeService>();

        services.AddSingleton<IArtifactStorage>(new S3ArtifactStorage(S3FileStorage.CreateClient(storageOptions), storageOptions));
        services.AddScoped<IArtifactIntakeService, ArtifactIntakeService>();
        services.AddScoped<PackageReader>();

        // Pulled packages: no proxy, no automatic redirects, internal addresses refused at connect time.
        services.AddHttpClient(ArtifactDownloader.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(30))
                .ConfigurePrimaryHttpMessageHandler(ArtifactDownloader.CreateHandler);
        services.AddScoped<ArtifactDownloader>();

        services.AddScoped<AppleTestFlightClient>();

        if (string.Equals(intakeOptions.AppleUploadMode, IntakeOptions.AppleUploadExternal, StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IAppleBuildUploader, ExternalAppleBuildUploader>();
        else
            services.AddScoped<IAppleBuildUploader, AppleBuildUploader>();

        services.AddScoped<IStoreJobHandler, ProcessArtifactJobHandler>();

        services.AddScoped<IntakeCleaner>();
        services.AddMilvaCronJob<IntakeCleanupJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"30 * * * *"; // At minute 30 of every hour
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
