using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.UserDtos;
using Storeasy.Application.Features.Users.CreateUser;
using Storeasy.Application.Features.Users.DeleteUser;
using Storeasy.Application.Features.Users.GetUserDetail;
using Storeasy.Application.Features.Users.GetUserList;
using Storeasy.Application.Features.Users.UpdateUser;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// User endpoints.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
[UserTypeAuth(UserType.Manager)]
public class UsersController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Gets users.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.UserManagement.List)]
    [HttpPatch]
    public Task<ListResponse<UserListDto>> GetUsersAsync(GetUserListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Get user according to user id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.UserManagement.Detail)]
    [HttpGet("user")]
    public Task<Response<UserDetailDto>> GetUserAsync([FromQuery] GetUserDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds user.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.UserManagement.Create)]
    [HttpPost("user")]
    public Task<Response<int>> AddUserAsync(CreateUserCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Updates user. Only the fields that are sent as isUpdated true are updated.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.UserManagement.Update)]
    [HttpPut("user")]
    public Task<Response<int>> UpdateUserAsync(UpdateUserCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes user.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.UserManagement.Delete)]
    [HttpDelete("user")]
    public Task<Response<int>> RemoveUserAsync([FromQuery] DeleteUserCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}