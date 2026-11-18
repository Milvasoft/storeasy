using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Protocol;
using Storeasy.Api.Mcp;
using Storeasy.Api.Middlewares;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using System.Globalization;

namespace Storeasy.Api.AppStartup;

public static partial class StartupExtensions
{
    /// <summary>
    /// Authorization policy of <c>/mcp</c>: a personal access token (authenticated by the storeasy token scheme only, so a
    /// web session JWT is a 401).
    /// </summary>
    public const string McpPolicy = "storeasy-mcp";

    /// <summary>
    /// Adds the remote MCP server (docs/urun-plani.md 2.6, K9): Streamable HTTP, stateless (no session affinity, no
    /// <c>Mcp-Session-Id</c>), tools from <see cref="McpToolCatalog"/>, every call a MediatR request in the HTTP request's
    /// scope.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddStoreasyMcp(this IServiceCollection services)
    {
        services.AddMcpServer(options =>
                {
                    options.ServerInfo = new Implementation { Name = "storeasy", Version = typeof(StartupExtensions).Assembly.GetName().Version?.ToString(3) ?? "1.0.0" };
                    options.ServerInstructions = "storeasy manages App Store Connect, Google Play and Microsoft Store apps of one organization. "
                                                 + "Start with apps_summary. Store writes are always plans: a *_plan tool returns a diff and a plan id, "
                                                 + "plans_approve applies it. Review submission and production need confirm: true and the typed confirmText. "
                                                 + "File arguments take asset ids; the local storeasy-mcp bridge turns local paths into them.";
                })
                .WithHttpTransport(options => options.Stateless = true)
                .WithListToolsHandler(McpToolInvoker.ListToolsAsync)
                .WithCallToolHandler(McpToolInvoker.CallToolAsync);

        services.AddAuthorizationBuilder()
                .AddPolicy(McpPolicy, policy => policy.AddAuthenticationSchemes(TokenClaimNames.AuthenticationScheme)
                                                      .RequireAuthenticatedUser()
                                                      .RequireClaim(TokenClaimNames.Kind, ((int)TokenKind.PersonalAccessToken).ToString(CultureInfo.InvariantCulture)));

        return services;
    }

    /// <summary>
    /// Maps the remote MCP at <c>/mcp</c> behind <see cref="McpPolicy"/>.
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static WebApplication MapStoreasyMcp(this WebApplication app)
    {
        app.MapMcp(StoreasyTokenMiddleware.McpPath).RequireAuthorization(McpPolicy);

        return app;
    }
}
