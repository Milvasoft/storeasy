using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.ChangePlans.ApplyChangePlan;
using Storeasy.Application.Features.Releases.GetPlayTesters;
using Storeasy.Application.Features.Releases.GetReleaseOverview;
using Storeasy.Application.Features.Releases.PlanPlayTesters;
using Storeasy.Application.Features.Releases.PlanRelease;
using Storeasy.Application.Features.Releases.PlanRollout;
using Storeasy.Application.Features.Releases.SubmitForReview;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Release endpoints: versions and tracks, release type and phased release, track releases with staged rollout and countries, rollout changes, testers and submission for review (typed confirmation, SubmitForReview).
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ReleasesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads versions (App Store) or tracks (Play) live.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<ReleaseOverviewDto>> GetReleasesAsync([FromQuery] GetReleaseOverviewQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans release settings (App Store) or a track release (Play).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("plan")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanReleaseAsync(PlanReleaseCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves a release plan (production needs ReleaseProduction and the typed confirmation) and queues its apply job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apply")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApplyReleaseAsync(ApplyChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans a staged or phased rollout change (DryRun false approves at once).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("rollout")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanRolloutAsync(PlanRolloutCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Submits the version for App Review behind the typed confirmation (Owner, Admin).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("submit-for-review")]
    [Idempotent]
    [RateLimit(30, 3600, "submit-for-review")]
    public Task<Response<ChangePlanDto>> SubmitForReviewAsync(SubmitForReviewCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the tester groups of a Play testing track.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("testers")]
    public Task<Response<PlayTestersDto>> GetTestersAsync([FromQuery] GetPlayTestersQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans the tester groups of a Play testing track.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("testers")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanTestersAsync(PlanPlayTestersCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
