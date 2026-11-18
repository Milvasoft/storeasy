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
using Storeasy.Application.Features.Account.ForgotPassword;
using Storeasy.Application.Features.Account.GetAccountNotifications;
using Storeasy.Application.Features.Account.Login;
using Storeasy.Application.Features.Account.Logout;
using Storeasy.Application.Features.Account.MarkNotificationsAsSeen;
using Storeasy.Application.Features.Account.RefreshLogin;
using Storeasy.Application.Features.Account.Register;
using Storeasy.Application.Features.Account.ResendEmailVerification;
using Storeasy.Application.Features.Account.ResetPassword;
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
    public static List<string> LoginEndpointPaths { get; } = ["login", "refresh", "register", "email/verify", "password/forgot", "password/reset"];

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