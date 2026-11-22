using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Features.Winget.AddWingetPackage;
using Storeasy.Application.Features.Winget.GetWingetManifest;
using Storeasy.Application.Features.Winget.GetWingetPackage;
using Storeasy.Application.Features.Winget.GetWingetPullRequestList;
using Storeasy.Application.Features.Winget.PlanWingetChange;
using Storeasy.Application.Features.Winget.SyncWinget;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// winget (Windows Package Manager community repository): packages, their manifests in winget-pkgs, pull request plans
/// and the review state of the pull requests. Connections are added like every store's (<c>POST connections</c>).
/// </summary>
/// <param name="mediator"></param>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class WingetController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Adds a package by its identifier to a new or an existing app and queues its sync.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("package")]
    [RateLimit(60, 3600, "winget-package-add")]
    public Task<Response<WingetPackageAddedDto>> AddPackageAsync(AddWingetPackageCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The package as the last sync read it, with its pull requests and the installers storeasy knows for the app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("package")]
    public Task<Response<WingetPackageDto>> GetPackageAsync([FromQuery] GetWingetPackageQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The manifests of one version, read live from winget-pkgs, with storeasy's checks.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("manifest")]
    [RateLimit(120, 3600, "winget-manifest")]
    public Task<Response<WingetManifestDto>> GetManifestAsync([FromQuery] GetWingetManifestQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The pull requests of a package and their review state.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("pull-requests")]
    public Task<Response<List<WingetPullRequestDto>>> GetPullRequestsAsync([FromQuery] GetWingetPullRequestListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a pull request (new version or locale texts); approving the plan opens it.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("change")]
    [RateLimit(120, 3600, "winget-change")]
    public Task<Response<ChangePlanDto>> PlanChangeAsync(PlanWingetChangeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of one package.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "winget-sync")]
    public Task<Response<long>> SyncAsync(SyncWingetCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
