using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Aso.Impact;
using Storeasy.Infrastructure.Jobs;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// Registrations of ASO keyword research and rank tracking (configuration section <see cref="AsoOptions.SectionName"/>).
/// </summary>
public static class AsoServiceCollectionExtensions
{
    /// <summary>
    /// Adds the store sources, the researcher, the rank recorder, the rank series reader, the hourly rank job and the before
    /// and after impact analyzer with its event sources.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddAsoServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        var options = configurationManager.GetSection(AsoOptions.SectionName).Get<AsoOptions>() ?? new AsoOptions();

        services.AddSingleton(options);

        // One client for every store source; tests replace its handler. The per request timeout is AsoOptions'.
        services.AddHttpClient(AsoHttp.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.RequestTimeoutSeconds * 2)))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = System.Net.DecompressionMethods.All });

        // The gate (one request at a time per host) and the answer cache are shared by the whole process.
        services.AddSingleton<AsoHttp>();

        services.AddScoped<IAsoSuggestionSource, AppleSearchHintsSource>();
        services.AddScoped<IAsoSuggestionSource, GooglePlaySuggestSource>();
        services.AddScoped<IAsoSearchSource, ITunesSearchSource>();
        services.AddScoped<IAsoSearchSource, GooglePlaySearchSource>();
        services.AddScoped<IAsoPopularityProvider, NullAsoPopularityProvider>();

        services.AddScoped<AsoKeywordResearcher>();
        services.AddScoped<IAsoKeywordResearcher>(provider => provider.GetRequiredService<AsoKeywordResearcher>());
        services.AddScoped<IAsoRankRecorder, AsoRankRecorder>();
        services.AddScoped<IAsoRankSeries, AsoRankSeriesReader>();

        // Before and after impact: every IImpactEventSource is read and merged, so a new kind of change plugs in with one
        // more registration here.
        services.AddScoped<IImpactEventSource, ChangePlanImpactSource>();
        services.AddScoped<IImpactEventSource, ReleaseImpactSource>();
        services.AddScoped<IImpactEventSource, ExperimentImpactSource>();
        services.AddScoped<IAsoImpactAnalyzer, AsoImpactAnalyzer>();

        // Hourly: a day's checks are spread over the runs (each checks what is not yet checked today, up to its limit), and
        // a check that failed is tried again an hour later.
        services.AddMilvaCronJob<AsoRankJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = "20 * * * *";
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
