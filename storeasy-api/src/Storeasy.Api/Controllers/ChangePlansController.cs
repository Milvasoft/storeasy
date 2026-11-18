using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.ChangePlans.ApplyChangePlan;
using Storeasy.Application.Features.ChangePlans.DiscardChangePlan;
using Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;
using Storeasy.Application.Features.ChangePlans.GetChangePlanList;
using Storeasy.Application.Features.ChangePlans.RejectChangePlan;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Change plan endpoints (docs/urun-plani.md 2.2): the approval queue, one plan with its diff, approve (which queues the apply job), reject and discard. Every store write of storeasy goes through a plan.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/plans")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ChangePlansController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the organization's change plans (default: pending, the approval queue).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<ChangePlanListDto>> GetPlansAsync(GetChangePlanListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one plan with its diff, remarks, store calls and state.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("plan")]
    public Task<Response<ChangePlanDto>> GetPlanAsync([FromQuery] GetChangePlanDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves a pending plan (the plan's own permission and typed confirmation) and queues its apply job. 409 when the store changed since the plan was made, 410 when it expired.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("approve")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApprovePlanAsync(ApplyChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Rejects a pending plan with a reason; nothing is written.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("reject")]
    public Task<Response<ChangePlanDto>> RejectPlanAsync(RejectChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Withdraws a pending plan; nothing is written.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("discard")]
    public Task<Response<ChangePlanDto>> DiscardPlanAsync(DiscardChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
