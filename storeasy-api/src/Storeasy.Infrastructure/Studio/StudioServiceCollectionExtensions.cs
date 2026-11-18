using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Storage;
using Storeasy.Infrastructure.Studio.Rendering;

namespace Storeasy.Infrastructure.Studio;

/// <summary>
/// Registrations of the mockup studio (phase 6).
/// </summary>
public static class StudioServiceCollectionExtensions
{
    /// <summary>
    /// Adds the studio: screenshot sizes from the rules catalog, the SkiaSharp renderer with the embedded fonts, server side
    /// object reads and writes with the raw screenshot cache, the starter templates, the caption translator (a no-op unless
    /// another implementation is registered first) and the export job handler.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddStudioServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        var studioOptions = configurationManager.GetSection(StudioOptions.SectionName).Get<StudioOptions>() ?? new StudioOptions();
        var storageOptions = configurationManager.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        services.AddSingleton(studioOptions);

        services.AddSingleton<IScreenshotSizeSource>(new FileScreenshotSizeSource(studioOptions.RulesPath));
        services.AddSingleton(_ => new StudioFontLibrary(StudioFontLibrary.ResolveFolder(studioOptions.FontsPath)));
        services.AddSingleton<IMockupRenderer, SkiaMockupRenderer>();
        services.AddSingleton<IStarterTemplateCatalog>(_ => StarterTemplateCatalog.LoadEmbedded());

        services.AddSingleton<IObjectContentStorage>(new S3ObjectContentStorage(S3FileStorage.CreateClient(storageOptions), storageOptions));
        services.AddSingleton<IStudioAssetReader>(sp => new StudioAssetReader(sp.GetRequiredService<IObjectContentStorage>(), Math.Max(1, studioOptions.AssetCacheMegabytes) * 1024L * 1024L));

        services.TryAddSingleton<ITitleTranslator, NoOpTitleTranslator>();

        services.AddScoped<IStoreJobHandler, MockupExportJobHandler>();

        return services;
    }
}
