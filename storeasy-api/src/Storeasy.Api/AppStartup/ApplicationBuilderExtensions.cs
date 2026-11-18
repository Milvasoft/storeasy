using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.MultiLanguage.Manager;
using Milvasoft.Localization;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Infrastructure.Persistence.Context;
using Scalar.AspNetCore;
using System.Globalization;

namespace Storeasy.Api.AppStartup;

/// <summary>
/// Application builder and service collection extensions.
/// </summary>
public static partial class StartupExtensions
{
    /// <summary>
    /// Maps the OpenAPI document and the Scalar UI, only when <c>MILVA_ENV</c> is <c>dev</c>
    /// (<see cref="StoreasyExtensions.IsCurrentEnvDevelopment"/>). The page carries no token: a developer signs in
    /// through <c>account/login</c> and pastes the access token into the UI.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseScalarWithOpenApi(this WebApplication app)
    {
        if (!StoreasyExtensions.IsCurrentEnvDevelopment())
            return app;

        // Documentation is reachable without a token (deny-by-default fallback policy); it is mapped only in development.
        app.MapOpenApi(GlobalConstant.RoutePrefix + "/docs/{documentName}/docs.json").AllowAnonymous();

        app.MapScalarApiReference(endpointPrefix: $"/{GlobalConstant.RoutePrefix}/documentation", options =>
        {
            options.WithOpenApiRoutePattern($"/{GlobalConstant.RoutePrefix}/docs/v1.0/docs.json");
            options.WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Axios);

            //UI
            options.WithTitle("Storeasy Api Reference")
                       .WithFavicon("https://demo.milvasoft.com/api/favicon.ico")
                       .WithDocumentDownloadType(DocumentDownloadType.None)
                       .EnableDarkMode()
                       .AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme)
                       .WithCustomCss(".darklight-reference-promo { display: none !important; } .darklight-reference { padding-bottom: 15px !important; } .open-api-client-button { display: none !important; }");
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Adds the required middleware to use the localization. Configures the options before add..
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseRequestLocalization(this WebApplication app)
    {
        var supportedCultures = LanguagesSeed.Seed.Select(i => new CultureInfo(i.Code)).ToArray();

        var defaultLanguageCode = LanguagesSeed.Seed.First(l => l.IsDefault).Code;

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(defaultLanguageCode),
            SupportedCultures = supportedCultures,
            SupportedUICultures = supportedCultures,
            ApplyCurrentCultureToResponseHeaders = true
        };

        var defaultCulture = new CultureInfo(defaultLanguageCode);

        CultureInfo.CurrentCulture = defaultCulture;
        CultureInfo.CurrentUICulture = defaultCulture;
        CultureInfo.DefaultThreadCurrentCulture = defaultCulture;

        // Background work (jobs, notifications, the e-mail outbox) runs outside a request, so the request localization
        // middleware never sets its UI culture. Without this default it falls back to the host's, which is the
        // invariant culture in Linux containers: resource lookups then return the bare keys instead of texts.
        CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;

        _ = new CultureSwitcher(defaultLanguageCode);

        return app.UseRequestLocalization(options);
    }

    /// <summary>
    /// Adds the required middleware to serve static files. Configures the options before adding.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseStaticFiles(this WebApplication app)
    {
        app.UseStaticFiles($"/{GlobalConstant.RoutePrefix}");

        return app;
    }

    /// <summary>
    /// Creates database if not exists.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> instance.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> instance.</returns>
    public static async Task<IApplicationBuilder> CreateDatabaseIfNotExistsAsync(this WebApplication app)
    {
        using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
        {
            var factory = serviceScope.ServiceProvider.GetRequiredService<StoreasyDbContextScopedFactory>();
            await factory.CreateDbContext().Database.MigrateAsync();
        }

        return app;
    }
}
