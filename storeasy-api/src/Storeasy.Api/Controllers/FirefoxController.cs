using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Features.Firefox.AddFirefoxAddon;
using Storeasy.Application.Features.Firefox.GetFirefoxAddon;
using Storeasy.Application.Features.Firefox.PlanFirefoxChange;
using Storeasy.Application.Features.Firefox.SyncFirefox;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Firefox Add-ons (addons.mozilla.org API v5): the account's add-ons (added by the connection's sync, or one by id, guid or
/// slug), their listing, versions and review state, and plans that edit the listing, previews and icon, submit a new version
/// on the listed or unlisted channel, or edit release notes. Connections are added like every store's
/// (<c>POST connections</c>); ratings come through the shared reviews pages.
/// </summary>
/// <param name="mediator"></param>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class FirefoxController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Adds one add-on by its AMO id, guid or slug to a new or an existing app and queues its sync.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("addon")]
    [RateLimit(60, 3600, "firefox-addon-add")]
    public Task<Response<FirefoxAddonAddedDto>> AddAddonAsync(AddFirefoxAddonCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The add-on: what the last sync saved, a live read of the listing, previews and versions, and the .xpi packages intake
    /// received for the app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("addon")]
    public Task<Response<FirefoxAddonDto>> GetAddonAsync([FromQuery] GetFirefoxAddonQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a write (listing, new version, previews, icon, release notes); approving the plan sends it.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("change")]
    [RateLimit(120, 3600, "firefox-change")]
    public Task<Response<ChangePlanDto>> PlanChangeAsync(PlanFirefoxChangeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of one add-on.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "firefox-sync")]
    public Task<Response<long>> SyncAsync(SyncFirefoxCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
