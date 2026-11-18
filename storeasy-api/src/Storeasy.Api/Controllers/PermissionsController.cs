using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.PermissionDtos;
using Storeasy.Application.Features.Permissions.GetPermissionList;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Attributes;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.PermissionManager;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Permission endpoints.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class PermissionsController(IMediator mediator, IPermissionManager permissionManager) : ControllerBase
{
    private readonly IMediator _mediator = mediator;
    private readonly IPermissionManager _permissionManager = permissionManager;

    /// <summary>
    /// Get permissions in the system.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.PermissionManagement.List)]
    [HttpPatch]
    public Task<ListResponse<PermissionListDto>> GetPermissionsAsync(GetPermissionListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Migrates permissions to database.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [Auth(PermissionCatalog.App.SuperAdmin)]
    [HttpPut("migrate")]
    public Task<Response<string>> MigratePermissionsAsync(CancellationToken cancellation) => _permissionManager.MigratePermissionsAsync(cancellation);
}