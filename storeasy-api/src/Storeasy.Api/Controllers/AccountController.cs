using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Features.Account.AccountDetail;
using Storeasy.Application.Features.Account.ChangePassword;
using Storeasy.Application.Features.Account.DeleteNotifications;
using Storeasy.Application.Features.Account.DeleteWebPushSubscription;
using Storeasy.Application.Features.Account.ForgotPassword;
using Storeasy.Application.Features.Account.GetAccountNotifications;
using Storeasy.Application.Features.Account.GetSessionList;
using Storeasy.Application.Features.Account.GetWebPushSubscriptionList;
using Storeasy.Application.Features.Account.GoogleSignIn;
using Storeasy.Application.Features.Account.Login;
using Storeasy.Application.Features.Account.Logout;
using Storeasy.Application.Features.Account.MarkNotificationsAsSeen;
using Storeasy.Application.Features.Account.RefreshLogin;
using Storeasy.Application.Features.Account.Register;
using Storeasy.Application.Features.Account.ResendEmailVerification;
using Storeasy.Application.Features.Account.ResetPassword;
using Storeasy.Application.Features.Account.RevokeOtherSessions;
using Storeasy.Application.Features.Account.RevokeSession;
using Storeasy.Application.Features.Account.SaveWebPushSubscription;
using Storeasy.Application.Features.Account.SendTestWebPush;
using Storeasy.Application.Features.Account.StartGoogleSignIn;
using Storeasy.Application.Features.Account.UpdateProfile;
using Storeasy.Application.Features.Account.VerifyEmail;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Account endpoints.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AccountController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Anonymous endpoint paths where a stale or invalid bearer token is ignored instead of answered with 401/419.
    /// </summary>
    public static List<string> LoginEndpointPaths { get; } = ["login", "refresh", "register", "email/verify", "password/forgot", "password/reset", "login/google/start", "login/google", "invitation/preview", "public-settings"];

    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// User login operation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns>Token information</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [RateLimit(10, 60, "account-login")]
    public Task<Response<LoginResponseDto>> LoginAsync(LoginCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts a Google sign in (anonymous; OpenID Connect authorization code with PKCE). Returns Google's authorization
    /// address and the state; the browser goes there and Google sends it back to the web app's <c>/auth/google/callback</c>
    /// page. 404 <c>GoogleSignInDisabled</c> when <c>Storeasy:Auth:Google</c> is not configured.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("login/google/start")]
    [AllowAnonymous]
    [RateLimit(20, 60, "account-login-google-start")]
    public Task<Response<GoogleSignInStartDto>> StartGoogleSignInAsync(StartGoogleSignInCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Completes a Google sign in with the code and state of the callback (anonymous). Signs in the linked account, links
    /// the account of the address Google verified, or creates one when the registration mode allows it. Returns the same
    /// token pair as the password login.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("login/google")]
    [AllowAnonymous]
    [RateLimit(10, 60, "account-login-google")]
    public Task<Response<LoginResponseDto>> GoogleSignInAsync(GoogleSignInCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// User refresh login operation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("login/refresh")]
    [AllowAnonymous]
    [RateLimit(30, 60, "account-login-refresh")]
    public Task<Response<LoginResponseDto>> RefreshLoginAsync(RefreshLoginCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates an account that signs in with its e-mail (anonymous). Allowed by <c>Storeasy:Registration:Mode</c>: Open,
    /// InviteOnly (an invitation token for the same e-mail is required and accepted) or Closed. A verification e-mail is
    /// sent; until it is verified the account can sign in but cannot create organizations or store connections.
    /// Returns the new user id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [RateLimit(10, 3600, "account-register")]
    public Task<Response<int>> RegisterAsync(RegisterCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Verifies the e-mail address with the token from the verification e-mail (anonymous, single use, valid 48 hours).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("email/verify")]
    [AllowAnonymous]
    [RateLimit(30, 3600, "account-email-verify")]
    public Task<Response> VerifyEmailAsync(VerifyEmailCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sends a new verification e-mail to the signed in user; earlier links stop working.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPost("email/verify/resend")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    [RateLimit(5, 3600, "account-email-verify-resend")]
    public Task<Response> ResendEmailVerificationAsync(ResendEmailVerificationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sends a password reset link when an account uses the e-mail (anonymous). Always answers success, so it does not
    /// reveal who has an account.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    [RateLimit(5, 3600, "account-password-forgot")]
    public Task<Response> ForgotPasswordAsync(ForgotPasswordCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sets a new password with the token from the reset e-mail (anonymous, single use, valid 1 hour). Every session and
    /// access token of the account stops working.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("password/reset")]
    [AllowAnonymous]
    [RateLimit(10, 3600, "account-password-reset")]
    public Task<Response> ResetPasswordAsync(ResetPasswordCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// User logout operation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    [HttpPost("logout")]
    public Task<Response> LogoutAsync(LogoutCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// User's own password change operation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPut("password/change")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response> ChangePasswordAsync(ChangePasswordCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// User can access his/her account information through this endpoint. The account is always the caller's own (taken from the token).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpGet("detail")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<AccountDetailDto>> AccountDetailsAsync([FromQuery] AccountDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Updates the caller's own name, surname and interface language.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPut("profile")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<int>> UpdateProfileAsync(UpdateProfileCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the caller's active sessions (signed in devices) with the last seen time.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpGet("sessions")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<List<AccountSessionDto>>> GetSessionsAsync(CancellationToken cancellation) => _mediator.Send(new GetSessionListQuery(), cancellation);

    /// <summary>
    /// Signs out one session: its refresh and access tokens stop working and its live connection closes.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpDelete("session")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<long>> RevokeSessionAsync(RevokeSessionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Signs out every other session of the caller.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpDelete("sessions/others")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<int>> RevokeOtherSessionsAsync(CancellationToken cancellation) => _mediator.Send(new RevokeOtherSessionsCommand(), cancellation);

    /// <summary>
    /// Lists the caller's browser push subscriptions.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpGet("push/subscriptions")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<List<WebPushSubscriptionDto>>> GetWebPushSubscriptionsAsync(CancellationToken cancellation) => _mediator.Send(new GetWebPushSubscriptionListQuery(), cancellation);

    /// <summary>
    /// Saves the caller's browser push subscription (the browser's <c>PushSubscription.toJSON()</c> fields). 404
    /// <c>WebPushDisabled</c> when <c>Storeasy:WebPush</c> has no key pair.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPost("push/subscription")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    [RateLimit(30, 3600, "account-push-subscribe")]
    public Task<Response<int>> SaveWebPushSubscriptionAsync(SaveWebPushSubscriptionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes one of the caller's browser push subscriptions (by id or address).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpDelete("push/subscription")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response<int>> DeleteWebPushSubscriptionAsync(DeleteWebPushSubscriptionCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sends a test push to the caller's browsers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPost("push/test")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    [RateLimit(5, 3600, "account-push-test")]
    public Task<Response<int>> SendTestWebPushAsync(SendTestWebPushCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Gets account notifications.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPatch("notifications")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<ListResponse<AccountNotificationDto>> GetNotificationsAsync(GetAccountNotificationsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Marks notifications as seen.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpPut("notifications/seen")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response> MarkNotificationsAsSeenAsync(MarkNotificationsAsSeenCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Deletes notifications.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth]
    [HttpDelete("notifications")]
    [UserTypeAuth(UserType.Manager | UserType.AppUser)]
    public Task<Response> DeleteNotificationsAsync(DeleteNotificationsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}