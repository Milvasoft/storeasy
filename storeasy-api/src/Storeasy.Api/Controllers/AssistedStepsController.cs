using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Features.AssistedSteps.CompleteAssistedStep;
using Storeasy.Application.Features.AssistedSteps.GetAssistedStepDetail;
using Storeasy.Application.Features.AssistedSteps.GetAssistedStepList;
using Storeasy.Application.Features.AssistedSteps.HandOffAssistedStep;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Assisted step endpoints (docs/urun-plani.md 11.4): store work without an API, its checklist and prepared answers, the hand-off to a browser agent in the member's own session and the recorded result. No store password is ever stored or asked for.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/assisted-steps")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AssistedStepsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the assisted steps of the organization's apps (created on first read).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<Response<List<AssistedStepListDto>>> GetStepsAsync(GetAssistedStepListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one step with its checklist, store page and prepared answers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("step")]
    public Task<Response<AssistedStepDetailDto>> GetStepAsync([FromQuery] GetAssistedStepDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Hands a step off to a browser agent: the structured step, without any password.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("step/handoff")]
    public Task<Response<AssistedStepHandOffDto>> HandOffAsync(HandOffAssistedStepCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Records the progress or result of a step.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("step/complete")]
    public Task<Response<int>> CompleteAsync(CompleteAssistedStepCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
