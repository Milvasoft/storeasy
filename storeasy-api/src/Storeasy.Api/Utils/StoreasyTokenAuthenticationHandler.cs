using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Milvasoft.Components.Rest.Enums;
using Storeasy.Api.Middlewares;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Domain.Tokens;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Storeasy.Api.Utils;

/// <summary>
/// Authenticates <c>Authorization: Bearer</c> values with a storeasy token prefix (<c>sea_</c>, <c>sci_</c>, <c>sut_</c>)
/// through <see cref="IApiTokenAuthenticator"/>. The principal carries the owner of a personal access token (<c>uid</c>,
/// name, user type, but no platform permissions) and the token's claims (<see cref="TokenClaimNames"/>). A failed token is
/// a 401 envelope whose key says why (<c>Unauthorized</c>, <c>TokenExpired</c>, <c>CiTokenExpired</c>,
/// <c>TokenRevoked</c>); the value itself never appears in a response or a log.
/// </summary>
/// <param name="options"></param>
/// <param name="logger"></param>
/// <param name="encoder"></param>
public sealed class StoreasyTokenAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// Policy scheme that forwards to this handler for storeasy tokens and to JWT bearer otherwise.
    /// </summary>
    public const string SelectorScheme = "StoreasyBearer";

    private const string _failureItemKey = "storeasy.token.failure";
    private const int _clientNameMaxLength = 200;

    /// <summary>
    /// Whether the request carries a storeasy token (selects this handler).
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public static bool HasStoreasyToken(HttpContext context) => TokenPrefixes.KindOf(BearerValue(context)) != null;

    /// <inheritdoc/>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = BearerValue(Context);

        if (TokenPrefixes.KindOf(token) == null)
            return AuthenticateResult.NoResult();

        var authenticator = Context.RequestServices.GetRequiredService<IApiTokenAuthenticator>();
        var userAgent = Request.Headers.UserAgent.ToString();
        var clientName = userAgent.Length > _clientNameMaxLength ? userAgent[.._clientNameMaxLength] : userAgent;

        var result = await authenticator.AuthenticateAsync(token, clientName, Context.RequestAborted);

        if (!result.Succeeded)
        {
            Context.Items[_failureItemKey] = result.FailureKey;

            return AuthenticateResult.Fail(result.FailureKey);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.UserName ?? result.Name ?? string.Empty),
            new(TokenClaimNames.Kind, ((int)result.Kind).ToString(CultureInfo.InvariantCulture)),
            new(TokenClaimNames.TokenId, result.TokenId.ToString(CultureInfo.InvariantCulture)),
            new(TokenClaimNames.OrganizationId, result.OrganizationId.ToString(CultureInfo.InvariantCulture)),
            new(TokenClaimNames.ReadOnly, result.ReadOnly.ToString()),
            new(TokenClaimNames.AllApps, result.AllApps.ToString()),
            new(TokenClaimNames.AppIds, string.Join(',', result.AppIds)),
            new(TokenClaimNames.Prefix, result.Prefix ?? string.Empty),
            new(TokenClaimNames.Name, result.Name ?? string.Empty),
        };

        if (result.UserId is { } userId)
            claims.Add(new Claim(GlobalConstant.UserIdClaimName, userId.ToString(CultureInfo.InvariantCulture)));

        if (result.UserType is { } userType)
            claims.Add(new Claim(GlobalConstant.UserTypeClaimName, ((byte)userType).ToString(CultureInfo.InvariantCulture)));

        if (result.TrustedPublisherId is { } trustedPublisherId)
            claims.Add(new Claim(TokenClaimNames.TrustedPublisherId, trustedPublisherId.ToString(CultureInfo.InvariantCulture)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    /// <inheritdoc/>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
            return Task.CompletedTask;

        var key = Context.Items.TryGetValue(_failureItemKey, out var failure) && failure is string text ? text : MessageKey.Unauthorized;

        return ExceptionMiddleware.RewriteResponseAsync(Context, key, StatusCodes.Status401Unauthorized, MessageType.Unauthorized);
    }

    /// <inheritdoc/>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
            return Task.CompletedTask;

        return ExceptionMiddleware.RewriteResponseAsync(Context, MessageKey.OrganizationForbidden, StatusCodes.Status403Forbidden, MessageType.Forbidden);
    }

    private static string BearerValue(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
