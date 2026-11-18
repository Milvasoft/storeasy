using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.ChangePlans.ApplyChangePlan;
using Storeasy.Application.Features.Media.GetMediaSets;
using Storeasy.Application.Features.Media.PlanMediaSet;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Screenshot and Play graphics endpoints: catalog evaluated classes per locale (required, filled by a fallback, missing) and change plans from uploaded assets (reserve, upload, commit, reorder when applied).
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ScreenshotsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the classes of one locale with the catalog requirement and the items.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("sets")]
    public Task<Response<MediaSetListDto>> GetSetsAsync([FromQuery] GetMediaSetsQuery request, CancellationToken cancellation) => _mediator.Send(request with { Kind = MediaKind.Screenshot }, cancellation);

    /// <summary>
    /// Plans one set from existing items and uploaded assets; files are checked against the catalog.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("plan")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanSetAsync(PlanMediaSetCommand request, CancellationToken cancellation) => _mediator.Send(request with { Kind = MediaKind.Screenshot }, cancellation);

    /// <summary>
    /// Approves a media plan and queues its apply job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apply")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApplySetAsync(ApplyChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
