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
using Storeasy.Application.Features.Account.GetAccountNotifications;
using Storeasy.Application.Features.Account.Login;
using Storeasy.Application.Features.Account.Logout;
using Storeasy.Application.Features.Account.MarkNotificationsAsSeen;
using Storeasy.Application.Features.Account.RefreshLogin;
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
    /// Not validate token endpoint paths.
    /// </summary>
    public static List<string> LoginEndpointPaths { get; } = ["login", "refresh"];

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