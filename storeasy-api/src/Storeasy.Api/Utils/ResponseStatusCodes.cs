using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Utils.Constants;
using System.Collections.Frozen;

namespace Storeasy.Api.Utils;

/// <summary>
/// Maps the message keys handlers put on a failed <see cref="IResponse"/> to the HTTP status code the
/// response should be sent with. Handlers keep returning the envelope (<c>IsSuccess=false</c> + messages);
/// this is the single place that decides whether that failure is a 400, 401, 403, 404, 409, 429, 500 or 503 on the wire.
/// Unknown keys fall back to 400 so a new message key can never accidentally produce a 200.
/// When a new message key is added and it is not a 400, it is added to this table too.
/// </summary>
public static class ResponseStatusCodes
{
    private static readonly FrozenDictionary<string, int> _byKey = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        // 401 - the caller is not (or no longer) authenticated.
        [MessageKey.Unauthorized] = StatusCodes.Status401Unauthorized,
        [MessageKey.SessionTimeout] = StatusCodes.Status401Unauthorized,

        // 403 - authenticated but not allowed (permissions, lockout).
        [MessageKey.Forbidden] = StatusCodes.Status403Forbidden,
        [MessageKey.Locked] = StatusCodes.Status403Forbidden,
        [MessageKey.LockedIndefinite] = StatusCodes.Status403Forbidden,
        [MessageKey.InvitationEmailMismatch] = StatusCodes.Status403Forbidden,
        [MessageKey.OrganizationForbidden] = StatusCodes.Status403Forbidden,
        [MessageKey.RegistrationClosed] = StatusCodes.Status403Forbidden,
        [MessageKey.RegistrationInvitationRequired] = StatusCodes.Status403Forbidden,
        [MessageKey.EmailNotVerified] = StatusCodes.Status403Forbidden,

        // 404 - the target does not exist (or is invisible to the caller, which is reported the same way).
        [MessageKey.UserNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.RoleNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.PageNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.FeatureFlagNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.StoreConnectionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.InvitationInvalid] = StatusCodes.Status404NotFound,
        [MessageKey.OrganizationMemberNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.OrganizationNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.StoreJobNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.AssetNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.AppNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.StoreAppNotFound] = StatusCodes.Status404NotFound,

        // 409 - the request conflicts with current state (duplicates, protected seed data).
        [MessageKey.UserExists] = StatusCodes.Status409Conflict,
        [MessageKey.EmailExists] = StatusCodes.Status409Conflict,
        [MessageKey.PostgreDuplicateDataException] = StatusCodes.Status409Conflict,
        [MessageKey.DefaultValueCannotModify] = StatusCodes.Status409Conflict,
        [MessageKey.IdempotencyKeyReused] = StatusCodes.Status409Conflict,
        [MessageKey.RequestInProgress] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationLastOwner] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationAlreadyMember] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationSlugTaken] = StatusCodes.Status409Conflict,
        [MessageKey.EmailAlreadyVerified] = StatusCodes.Status409Conflict,
        [MessageKey.StoreJobAlreadyFinished] = StatusCodes.Status409Conflict,
        [MessageKey.AssetNotUploaded] = StatusCodes.Status409Conflict,
        [MessageKey.StoreAppAlreadyAdded] = StatusCodes.Status409Conflict,
        [MessageKey.AppStoreAlreadyLinked] = StatusCodes.Status409Conflict,

        // 429 - the caller exceeded a rate limit (see RateLimitAttribute).
        [MessageKey.TooManyRequests] = StatusCodes.Status429TooManyRequests,

        // 500 - our fault, not the caller's.
        [MessageKey.UnhandledException] = StatusCodes.Status500InternalServerError,
        [MessageKey.PostgreBasedException] = StatusCodes.Status500InternalServerError,

        // 503 - the feature is switched off by an admin (see FeatureFlagAttribute).
        [MessageKey.FeatureDisabled] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.StorageNotConfigured] = StatusCodes.Status503ServiceUnavailable,

        // Mockup studio (phase 6).
        [MessageKey.MockupTemplateNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupStarterNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupExportNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupTemplateNameExists] = StatusCodes.Status409Conflict,
        [MessageKey.MockupTemplateChanged] = StatusCodes.Status409Conflict,
        [MessageKey.MockupExportNotReady] = StatusCodes.Status409Conflict,
        [MessageKey.TitleTranslatorUnavailable] = StatusCodes.Status503ServiceUnavailable,
    }.ToFrozenDictionary();

    /// <summary>
    /// Resolves the HTTP status code for a single message key. Unknown or empty keys map to 400.
    /// </summary>
    /// <param name="messageKey"></param>
    /// <returns></returns>
    public static int FromKey(string messageKey) => !string.IsNullOrEmpty(messageKey) && _byKey.TryGetValue(messageKey, out var status) ? status : StatusCodes.Status400BadRequest;

    /// <summary>
    /// Resolves the HTTP status code for a failed response from its messages. The first message that maps to a
    /// non-400 code wins (a response never carries more than one "reason" in practice); anything else is 400.
    /// Successful responses always resolve to 200 regardless of their messages.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static int FromResponse(IResponse response)
    {
        if (response == null || response.IsSuccess)
            return StatusCodes.Status200OK;

        if (response.Messages != null)
        {
            foreach (var message in response.Messages)
            {
                // ResponseInterceptor keeps the untranslated key in Key; before translation Message still holds it.
                var status = FromKey(string.IsNullOrEmpty(message.Key) ? message.Message : message.Key);

                if (status != StatusCodes.Status400BadRequest)
                    return status;
            }
        }

        return StatusCodes.Status400BadRequest;
    }
}
