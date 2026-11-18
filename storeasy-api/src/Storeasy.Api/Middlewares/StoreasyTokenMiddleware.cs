using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Middlewares;

/// <summary>
/// Runs after authentication for requests made with a storeasy token: refuses CI and upload tokens on endpoints not
/// marked <see cref="AllowMachineTokenAttribute"/> and every token on endpoints marked
/// <see cref="WebSessionOnlyAttribute"/> (403), and records the audit channel of the request: MCP for <c>/mcp</c>, API for
/// a personal access token, CI for a CI or upload token.
/// </summary>
/// <param name="next"></param>
public class StoreasyTokenMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Path of the remote MCP endpoint.
    /// </summary>
    public const string McpPath = "/mcp";

    private readonly RequestDelegate _next = next;

    /// <summary>
    /// Checks the token against the endpoint and sets the audit channel, then invokes the next middleware.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var currentToken = context.RequestServices.GetRequiredService<ICurrentToken>();

        if (currentToken.Kind is { } kind)
        {
            var metadata = context.GetEndpoint()?.Metadata;

            if (metadata?.GetMetadata<WebSessionOnlyAttribute>() != null)
                context.Response.ThrowWithForbidden(MessageKey.Forbidden);

            if (currentToken.IsMachine && metadata?.GetMetadata<AllowMachineTokenAttribute>() == null)
                context.Response.ThrowWithForbidden(MessageKey.OrganizationForbidden);

            var auditTrail = context.RequestServices.GetRequiredService<IAuditTrail>();

            auditTrail.Channel = context.Request.Path.StartsWithSegments(McpPath) ? ActionChannel.Mcp : kind == TokenKind.PersonalAccessToken ? ActionChannel.Api : ActionChannel.Ci;
        }

        await _next(context);
    }
}
