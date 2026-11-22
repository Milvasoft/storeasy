using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Chrome.AddChromeItem;
using Storeasy.Application.Features.Chrome.GetChromeItem;
using Storeasy.Application.Features.Chrome.PlanChromeChange;
using Storeasy.Application.Features.Chrome.SyncChrome;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Chrome Web Store (Chrome Web Store API v2): items added by extension id, their status, and plans that upload a package
/// and submit it, publish a staged revision, raise the deploy percentage, cancel a submission or roll back. Connections are
/// added like every store's (<c>POST connections</c>); reviews and metrics come through the shared review and metrics pages.
/// </summary>
/// <param name="mediator"></param>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ChromeController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Adds an item by its extension id to a new or an existing app and queues its sync.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("item")]
    [RateLimit(60, 3600, "chrome-item-add")]
    public Task<Response<ChromeItemAddedDto>> AddItemAsync(AddChromeItemCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The item as the last sync read it, with the extension packages intake received for the app.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("item")]
    public Task<Response<ChromeItemDto>> GetItemAsync([FromQuery] GetChromeItemQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a write (submit, publish staged, deploy percentage, cancel submission, rollback); approving the plan sends it.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("change")]
    [RateLimit(120, 3600, "chrome-change")]
    public Task<Response<ChangePlanDto>> PlanChangeAsync(PlanChromeChangeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of one item.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "chrome-sync")]
    public Task<Response<long>> SyncAsync(SyncChromeCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
