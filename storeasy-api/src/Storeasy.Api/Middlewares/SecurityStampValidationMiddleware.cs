using Storeasy.Api.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Extensions;

namespace Storeasy.Api.Middlewares;

/// <summary>
/// Enforces token revocation. Runs after authentication: for an authenticated request it compares the token's
/// session-stamp claim against the user's current stamp (Redis, with a database fallback). A mismatch means the
/// token was revoked (password change, role change, lock, deletion) and the request is rejected with 401. A token
/// without a stamp claim is rejected as well: every token this API mints (login, refresh, long-lived, documentation)
/// carries one, so a missing claim means a foreign token that could never be revoked.
/// Endpoints marked with <see cref="SkipSessionStampValidationAttribute"/> are not checked.
/// </summary>
/// <param name="next"></param>
public class SecurityStampValidationMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    /// <summary>
    /// Validates the session stamp of the authenticated caller, then invokes the next middleware.
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;

        // A storeasy token (personal access, CI, upload) is not a session: its own revocation is checked when it is authenticated.
        if (user?.Identity?.IsAuthenticated == true && !user.HasClaim(c => c.Type == TokenClaimNames.Kind) && context.GetEndpoint()?.Metadata.GetMetadata<SkipSessionStampValidationAttribute>() is null)
        {
            var stampClaim = user.FindFirst(GlobalConstant.SessionStampClaimName)?.Value;

            var hasUserId = int.TryParse(user.FindFirst(GlobalConstant.UserIdClaimName)?.Value, out var userId);

            // Fail closed: no stamp claim (or no user id) -> the token cannot be checked for revocation.
            if (string.IsNullOrEmpty(stampClaim) || !hasUserId)
                context.Response.ThrowWithUnauthorized(MessageKey.Unauthorized);

            var stampStore = context.RequestServices.GetRequiredService<ISecurityStampStore>();

            var valid = await stampStore.IsValidAsync(userId, stampClaim, context.RequestAborted);

            if (!valid)
                context.Response.ThrowWithUnauthorized(MessageKey.Unauthorized);
        }

        await _next(context);
    }
}
