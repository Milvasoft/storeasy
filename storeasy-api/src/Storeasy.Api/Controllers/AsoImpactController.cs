using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Features.AsoImpact.GetAsoImpact;
using Storeasy.Application.Features.AsoImpact.GetAsoImpactEvents;
using Storeasy.Application.Features.AsoImpact.GetAsoImpactRecent;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Before and after impact of store changes (docs/aso-impact.md): the changes of an app with the day each took effect,
/// the report of one change (store page metrics, conversion significance, keyword ranks, confounding changes) and the
/// organization's recent changes. Read only; computed from storeasy's rows on every call.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/aso-impact")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AsoImpactController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The changes of one app (applied store page plans, versions that went live), newest first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("events")]
    public Task<Response<List<AsoImpactEventDto>>> GetEventsAsync([FromQuery] GetAsoImpactEventsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The before and after report of one change.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<AsoImpactReportDto>> GetImpactAsync([FromQuery] GetAsoImpactQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The impact of the organization's recent changes.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("recent")]
    public Task<Response<AsoImpactRecentDto>> GetRecentAsync([FromQuery] GetAsoImpactRecentQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
