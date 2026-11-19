using FluentAssertions;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.UnitTests.ApiTests;

/// <summary>
/// <see cref="ResponseStatusCodes"/>: the single mapping from failure message keys to wire status codes.
/// </summary>
[Trait("Api Unit Tests", "Response status code mapping.")]
public class ResponseStatusCodesTests
{
    [Theory]
    [InlineData(MessageKey.Unauthorized, 401)]
    [InlineData(MessageKey.OrganizationRoleNotFound, 404)]
    [InlineData(MessageKey.SessionNotFound, 404)]
    [InlineData(MessageKey.PermissionEscalation, 403)]
    [InlineData(MessageKey.UserIsLastOwner, 409)]
    [InlineData(MessageKey.DefaultLanguageRequired, 409)]
    [InlineData(MessageKey.UnsupportedLanguageCannotBeDefault, 409)]
    [InlineData(MessageKey.OrganizationRoleInUse, 409)]
    [InlineData(MessageKey.OrganizationRoleNameExists, 409)]
    [InlineData(MessageKey.OrganizationRoleLimitReached, 409)]
    [InlineData(MessageKey.GoogleSignInFailed, 401)]
    [InlineData(MessageKey.GoogleEmailNotVerified, 403)]
    [InlineData(MessageKey.GoogleSignInDisabled, 404)]
    [InlineData(MessageKey.GoogleSignInUnavailable, 503)]
    [InlineData(MessageKey.WebPushDisabled, 404)]
    [InlineData(MessageKey.WebPushSubscriptionNotFound, 404)]
    [InlineData(MessageKey.GoogleSignInStateInvalid, 400)]
    [InlineData(MessageKey.OrganizationRolePermissionInvalid, 400)]
    [InlineData(MessageKey.SessionTimeout, 401)]
    [InlineData(MessageKey.Forbidden, 403)]
    [InlineData(MessageKey.Locked, 403)]
    [InlineData(MessageKey.LockedIndefinite, 403)]
    [InlineData(MessageKey.UserNotFound, 404)]
    [InlineData(MessageKey.RoleNotFound, 404)]
    [InlineData(MessageKey.PageNotFound, 404)]
    [InlineData(MessageKey.FeatureFlagNotFound, 404)]
    [InlineData(MessageKey.UserExists, 409)]
    [InlineData(MessageKey.EmailExists, 409)]
    [InlineData(MessageKey.PostgreDuplicateDataException, 409)]
    [InlineData(MessageKey.DefaultValueCannotModify, 409)]
    [InlineData(MessageKey.IdempotencyKeyReused, 409)]
    [InlineData(MessageKey.RequestInProgress, 409)]
    [InlineData(MessageKey.TooManyRequests, 429)]
    [InlineData(MessageKey.UnhandledException, 500)]
    [InlineData(MessageKey.PostgreBasedException, 500)]
    [InlineData(MessageKey.FeatureDisabled, 503)]
    [InlineData(MessageKey.RegistrationClosed, 403)]
    [InlineData(MessageKey.RegistrationInvitationRequired, 403)]
    [InlineData(MessageKey.EmailNotVerified, 403)]
    [InlineData(MessageKey.EmailAlreadyVerified, 409)]
    [InlineData(MessageKey.AccountTokenInvalid, 400)]
    [InlineData(MessageKey.ChangePlanNotFound, 404)]
    [InlineData(MessageKey.RuleNotFound, 404)]
    [InlineData(MessageKey.AssistedStepNotFound, 404)]
    [InlineData(MessageKey.PlanConflict, 409)]
    [InlineData(MessageKey.PlanNotPending, 409)]
    [InlineData(MessageKey.StoreAppNotConnected, 409)]
    [InlineData(MessageKey.PlanExpired, 410)]
    [InlineData(MessageKey.StoreRejected, 422)]
    [InlineData(MessageKey.MediaRejected, 422)]
    [InlineData(MessageKey.StoreUnreachable, 502)]
    [InlineData(MessageKey.RulesCatalogUnavailable, 503)]
    [InlineData(MessageKey.AiKeyNotSet, 409)]
    [InlineData(MessageKey.AiKeyRejected, 409)]
    [InlineData(MessageKey.AiRateLimited, 429)]
    [InlineData(MessageKey.AiReplyRefused, 422)]
    [InlineData(MessageKey.AiReplyFailed, 502)]
    [InlineData(MessageKey.AiUnavailable, 503)]
    [InlineData(MessageKey.PlanConfirmMismatch, 400)]
    [InlineData(MessageKey.FieldTooManyBytes, 400)]
    public void FromKey_ShouldMapKnownKeysToTheirStatus(string key, int expected)
    {
        ResponseStatusCodes.FromKey(key).Should().Be(expected);
    }

    [Theory]
    [InlineData("SomethingNobodyMapped")]
    [InlineData("")]
    [InlineData(null)]
    public void FromKey_ShouldFallBackTo400_ForUnknownOrEmptyKeys(string key)
    {
        ResponseStatusCodes.FromKey(key).Should().Be(400);
    }

    [Fact]
    public void FromKey_ShouldBeCaseSensitive()
    {
        // The dictionary is ordinal: a differently-cased key is not a known key and must not leak a 4xx/5xx.
        ResponseStatusCodes.FromKey(MessageKey.UserNotFound.ToUpperInvariant()).Should().Be(400);
    }

    [Fact]
    public void FromResponse_ShouldReturn200_ForSuccessfulResponseEvenWithErrorLookingMessages()
    {
        var response = Response.Success(MessageKey.UnhandledException);
        response.AddMessage(MessageKey.UserNotFound);

        ResponseStatusCodes.FromResponse(response).Should().Be(200);
    }

    [Fact]
    public void FromResponse_ShouldReturn200_ForNullResponse()
    {
        ResponseStatusCodes.FromResponse(null).Should().Be(200);
    }

    [Fact]
    public void FromResponse_ShouldReturn400_ForFailedResponseWithNullMessages()
    {
        var response = Response.Error();
        response.Messages = null;

        ResponseStatusCodes.FromResponse(response).Should().Be(400);
    }

    [Fact]
    public void FromResponse_ShouldReturn400_ForFailedResponseWithOnlyUnknownMessages()
    {
        var response = Response.Error("Title is required");
        response.AddMessage("Another validation message");

        ResponseStatusCodes.FromResponse(response).Should().Be(400);
    }

    [Fact]
    public void FromResponse_ShouldUseFirstNon400Message()
    {
        var response = Response.Error("validation text");
        response.AddMessage(MessageKey.UserNotFound);
        response.AddMessage(MessageKey.UnhandledException);

        ResponseStatusCodes.FromResponse(response).Should().Be(404);
    }

    [Fact]
    public void FromResponse_ShouldPreferKeyOverMessage_WhenKeyIsSet()
    {
        // After translation the Message holds human text and Key holds the message key.
        var response = Response.Error(new ResponseMessage(MessageKey.Forbidden, "You cannot do that", MessageType.Warning));

        ResponseStatusCodes.FromResponse(response).Should().Be(403);
    }

    [Fact]
    public void FromResponse_ShouldFallBackToMessage_WhenKeyIsEmpty()
    {
        var response = Response.Error(new ResponseMessage(string.Empty, MessageKey.UserExists, MessageType.Warning));

        ResponseStatusCodes.FromResponse(response).Should().Be(409);
    }

    [Fact]
    public void FromResponse_ShouldIgnoreNullMessageAndKey()
    {
        var response = Response.Error(new ResponseMessage(null, null, MessageType.Error));

        ResponseStatusCodes.FromResponse(response).Should().Be(400);
    }
}
