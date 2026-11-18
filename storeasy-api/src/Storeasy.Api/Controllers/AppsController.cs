using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Features.Apps.AddGoogleApp;
using Storeasy.Application.Features.Apps.GetAppDetail;
using Storeasy.Application.Features.Apps.GetAppList;
using Storeasy.Application.Features.Apps.LinkStoreApp;
using Storeasy.Application.Features.Apps.SyncApps;
using Storeasy.Application.Features.Apps.UnlinkStoreApp;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App endpoints: storeasy apps and their records in each store (App Store Connect, Google Play, later Microsoft Store),
/// with their builds. Store data arrives through sync jobs; nothing here calls a store in the request.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AppsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the organization's apps with their store records (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<AppListDto>> GetAppsAsync(GetAppListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one app with its store records and newest builds (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("app")]
    public Task<Response<AppDetailDto>> GetAppAsync([FromQuery] GetAppDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds a Google Play app by package name (editors and above). A sync job confirms it and reads its builds; follow the
    /// returned job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("google")]
    [RateLimit(60, 3600, "apps-add-google")]
    public Task<Response<AppAddedDto>> AddGoogleAppAsync(AddGoogleAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Moves a store record to another app (editors and above); at most one record per store in an app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/link")]
    public Task<Response<int>> LinkStoreAppAsync(LinkStoreAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Splits a store record off into its own app (editors and above).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("app/unlink")]
    public Task<Response<int>> UnlinkStoreAppAsync(UnlinkStoreAppCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of apps and builds for one connection or all (editors and above). Returns the job ids.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(30, 3600, "apps-sync")]
    public Task<Response<List<long>>> SyncAppsAsync(SyncAppsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
