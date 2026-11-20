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
        [MessageKey.GoogleSignInFailed] = StatusCodes.Status401Unauthorized,
        [MessageKey.Unauthorized] = StatusCodes.Status401Unauthorized,
        [MessageKey.SessionTimeout] = StatusCodes.Status401Unauthorized,
        [MessageKey.TokenExpired] = StatusCodes.Status401Unauthorized,
        [MessageKey.CiTokenExpired] = StatusCodes.Status401Unauthorized,
        [MessageKey.TokenRevoked] = StatusCodes.Status401Unauthorized,
        [MessageKey.OidcTokenInvalid] = StatusCodes.Status401Unauthorized,
        [MessageKey.WebhookSignatureInvalid] = StatusCodes.Status401Unauthorized,

        // 403 - authenticated but not allowed (permissions, lockout).
        [MessageKey.GoogleEmailNotVerified] = StatusCodes.Status403Forbidden,
        [MessageKey.PermissionEscalation] = StatusCodes.Status403Forbidden,
        [MessageKey.Forbidden] = StatusCodes.Status403Forbidden,
        [MessageKey.Locked] = StatusCodes.Status403Forbidden,
        [MessageKey.LockedIndefinite] = StatusCodes.Status403Forbidden,
        [MessageKey.InvitationEmailMismatch] = StatusCodes.Status403Forbidden,
        [MessageKey.OrganizationForbidden] = StatusCodes.Status403Forbidden,
        [MessageKey.RegistrationClosed] = StatusCodes.Status403Forbidden,
        [MessageKey.RegistrationInvitationRequired] = StatusCodes.Status403Forbidden,
        [MessageKey.SetupCodeInvalid] = StatusCodes.Status403Forbidden,
        [MessageKey.EmailNotVerified] = StatusCodes.Status403Forbidden,
        [MessageKey.AppForbidden] = StatusCodes.Status403Forbidden,
        [MessageKey.OidcClaimMismatch] = StatusCodes.Status403Forbidden,

        // 404 - the target does not exist (or is invisible to the caller, which is reported the same way).
        [MessageKey.OrganizationRoleNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.SessionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.GoogleSignInDisabled] = StatusCodes.Status404NotFound,
        [MessageKey.WebPushDisabled] = StatusCodes.Status404NotFound,
        [MessageKey.WebPushSubscriptionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.RuleNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.ChangePlanNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.VersionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.SavedAnswerNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.BuildNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.BuildPackageNotStored] = StatusCodes.Status404NotFound,
        [MessageKey.AssistedStepNotFound] = StatusCodes.Status404NotFound,
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
        [MessageKey.MonetizationProductNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MonetizationGroupNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MonetizationOfferNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MonetizationBasePlanNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MonetizationTesterNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MsSubmissionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MsFlightNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MsAddonNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.ArtifactSessionNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.PersonalAccessTokenNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.CiTokenNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.TrustedPublisherNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.ArtifactSourceNotFound] = StatusCodes.Status404NotFound,

        // 409 - the request conflicts with current state (duplicates, protected seed data).
        [MessageKey.OrganizationRoleInUse] = StatusCodes.Status409Conflict,
        [MessageKey.UserIsLastOwner] = StatusCodes.Status409Conflict,
        [MessageKey.DefaultLanguageRequired] = StatusCodes.Status409Conflict,
        [MessageKey.UnsupportedLanguageCannotBeDefault] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationRoleNameExists] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationRoleLimitReached] = StatusCodes.Status409Conflict,
        [MessageKey.PlanConflict] = StatusCodes.Status409Conflict,
        [MessageKey.PlanNotPending] = StatusCodes.Status409Conflict,
        [MessageKey.StoreAppNotConnected] = StatusCodes.Status409Conflict,
        [MessageKey.VersionNotEditable] = StatusCodes.Status409Conflict,
        [MessageKey.BuildNotValid] = StatusCodes.Status409Conflict,
        [MessageKey.UserExists] = StatusCodes.Status409Conflict,
        [MessageKey.EmailExists] = StatusCodes.Status409Conflict,
        [MessageKey.PostgreDuplicateDataException] = StatusCodes.Status409Conflict,
        [MessageKey.DefaultValueCannotModify] = StatusCodes.Status409Conflict,
        [MessageKey.IdempotencyKeyReused] = StatusCodes.Status409Conflict,
        [MessageKey.RequestInProgress] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationLastOwner] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationAlreadyMember] = StatusCodes.Status409Conflict,
        [MessageKey.OrganizationSlugTaken] = StatusCodes.Status409Conflict,
        [MessageKey.SetupAlreadyCompleted] = StatusCodes.Status409Conflict,
        [MessageKey.SingleTeamOrganizationExists] = StatusCodes.Status409Conflict,
        [MessageKey.RegistrationModeFixedInSingleTeam] = StatusCodes.Status409Conflict,
        [MessageKey.EmailAlreadyVerified] = StatusCodes.Status409Conflict,
        [MessageKey.StoreJobAlreadyFinished] = StatusCodes.Status409Conflict,
        [MessageKey.AssetNotUploaded] = StatusCodes.Status409Conflict,
        [MessageKey.StoreAppAlreadyAdded] = StatusCodes.Status409Conflict,
        [MessageKey.AppStoreAlreadyLinked] = StatusCodes.Status409Conflict,
        [MessageKey.MonetizationProductIdTaken] = StatusCodes.Status409Conflict,
        [MessageKey.MonetizationGroupExists] = StatusCodes.Status409Conflict,
        [MessageKey.MonetizationOfferExists] = StatusCodes.Status409Conflict,
        [MessageKey.MonetizationBasePlanExists] = StatusCodes.Status409Conflict,
        [MessageKey.MonetizationFamilySharingLocked] = StatusCodes.Status409Conflict,
        [MessageKey.MsSubmissionInProgress] = StatusCodes.Status409Conflict,
        [MessageKey.MsSubmissionModifiedOutsideApi] = StatusCodes.Status409Conflict,
        [MessageKey.MsSubmissionNotOurs] = StatusCodes.Status409Conflict,
        [MessageKey.MsNoPendingSubmission] = StatusCodes.Status409Conflict,
        [MessageKey.MsPlanStale] = StatusCodes.Status409Conflict,
        [MessageKey.MsPricingAssisted] = StatusCodes.Status409Conflict,
        [MessageKey.MsAddonAssisted] = StatusCodes.Status409Conflict,
        [MessageKey.MsApiRefused] = StatusCodes.Status409Conflict,
        [MessageKey.MsFirstSubmissionRequired] = StatusCodes.Status409Conflict,
        [MessageKey.MsInstallerNotSigned] = StatusCodes.Status409Conflict,
        [MessageKey.MsInstallerNotReady] = StatusCodes.Status409Conflict,
        [MessageKey.MsFlightNameTaken] = StatusCodes.Status409Conflict,
        [MessageKey.MsAddonProductIdTaken] = StatusCodes.Status409Conflict,
        [MessageKey.DeliveryStoreMismatch] = StatusCodes.Status409Conflict,
        [MessageKey.TokenNameExists] = StatusCodes.Status409Conflict,
        [MessageKey.ArtifactNotHeld] = StatusCodes.Status409Conflict,

        // 410 - the plan expired.
        [MessageKey.PlanExpired] = StatusCodes.Status410Gone,

        // 422 - the store refused, or nothing usable was sent.
        [MessageKey.StoreRejected] = StatusCodes.Status422UnprocessableEntity,
        [MessageKey.MediaRejected] = StatusCodes.Status422UnprocessableEntity,
        [MessageKey.WebhookPayloadInvalid] = StatusCodes.Status422UnprocessableEntity,
        [MessageKey.DownloadHostNotAllowed] = StatusCodes.Status422UnprocessableEntity,

        // 502 - the store did not answer.
        [MessageKey.StoreUnreachable] = StatusCodes.Status502BadGateway,

        // 429 - the caller exceeded a rate limit (see RateLimitAttribute).
        [MessageKey.TooManyRequests] = StatusCodes.Status429TooManyRequests,

        // 500 - our fault, not the caller's.
        [MessageKey.UnhandledException] = StatusCodes.Status500InternalServerError,
        [MessageKey.PostgreBasedException] = StatusCodes.Status500InternalServerError,

        // 503 - the feature is switched off by an admin (see FeatureFlagAttribute).
        [MessageKey.GoogleSignInUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.FeatureDisabled] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.SetupNotReady] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.StorageNotConfigured] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.StorageUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.RulesCatalogUnavailable] = StatusCodes.Status503ServiceUnavailable,

        // Mockup studio (phase 6).
        [MessageKey.MockupTemplateNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupStarterNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupExportNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.MockupTemplateNameExists] = StatusCodes.Status409Conflict,
        [MessageKey.MockupTemplateChanged] = StatusCodes.Status409Conflict,
        [MessageKey.MockupExportNotReady] = StatusCodes.Status409Conflict,
        [MessageKey.TitleTranslatorUnavailable] = StatusCodes.Status503ServiceUnavailable,

        // Phase 4 (events and tracking).
        [MessageKey.WebhookEndpointNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.WebhookEventNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.OrgWebhookNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.RejectionCaseNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.ReviewNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.SavedReplyNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.BetaGroupNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.BetaTesterNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.RuleProposalNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.WebhookEndpointExists] = StatusCodes.Status409Conflict,
        [MessageKey.WebhookEventNotReprocessable] = StatusCodes.Status409Conflict,
        [MessageKey.WebhookEndpointNotPingable] = StatusCodes.Status409Conflict,
        [MessageKey.OrgWebhookLimitReached] = StatusCodes.Status409Conflict,
        [MessageKey.RejectionCaseClosed] = StatusCodes.Status409Conflict,
        [MessageKey.RejectionCaseExists] = StatusCodes.Status409Conflict,
        [MessageKey.ReviewReplyInProgress] = StatusCodes.Status409Conflict,

        // AI reply drafts: a missing or rejected organization key is a settings conflict (never 401/403: those are about
        // the caller's own session); Anthropic's limits and outages keep their meaning.
        [MessageKey.AiKeyNotSet] = StatusCodes.Status409Conflict,
        [MessageKey.AiKeyRejected] = StatusCodes.Status409Conflict,
        [MessageKey.AiRateLimited] = StatusCodes.Status429TooManyRequests,
        [MessageKey.AiUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [MessageKey.AiReplyRefused] = StatusCodes.Status422UnprocessableEntity,
        [MessageKey.AiReplyFailed] = StatusCodes.Status502BadGateway,
        [MessageKey.AiSuggestionNotSupported] = StatusCodes.Status400BadRequest,
        [MessageKey.AiSuggestionLocaleNotFound] = StatusCodes.Status400BadRequest,
        [MessageKey.AiSuggestionRefused] = StatusCodes.Status422UnprocessableEntity,
        [MessageKey.AiSuggestionFailed] = StatusCodes.Status502BadGateway,
        [MessageKey.PreReviewNotFound] = StatusCodes.Status404NotFound,
        [MessageKey.PreReviewInProgress] = StatusCodes.Status409Conflict,
        [MessageKey.BuildExpired] = StatusCodes.Status409Conflict,
        [MessageKey.BetaReviewAlreadySubmitted] = StatusCodes.Status409Conflict,
        [MessageKey.RuleProposalDecided] = StatusCodes.Status409Conflict,
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
