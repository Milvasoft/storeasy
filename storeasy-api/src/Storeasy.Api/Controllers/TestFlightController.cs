using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Application.Features.TestFlight.CreateBetaGroup;
using Storeasy.Application.Features.TestFlight.GetBetaBuildList;
using Storeasy.Application.Features.TestFlight.GetBetaGroupList;
using Storeasy.Application.Features.TestFlight.GetBetaLocalizationList;
using Storeasy.Application.Features.TestFlight.GetBetaTesterList;
using Storeasy.Application.Features.TestFlight.SubmitBetaReview;
using Storeasy.Application.Features.TestFlight.SyncTestFlight;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroup;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroupBuilds;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroupTesters;
using Storeasy.Application.Features.TestFlight.UpdateBetaLocalization;
using Storeasy.Application.Features.TestFlight.UpdateWhatToTest;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// TestFlight of an App Store record: builds with their 90 day expiry, beta groups, testers, beta texts and beta app
/// review. Reads come from the last sync; writes are store jobs.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class TestFlightController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The TestFlight builds with expiry and beta review state.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("builds")]
    public Task<Response<List<BetaBuildDto>>> GetBuildsAsync([FromQuery] GetBetaBuildListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The beta groups.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("groups")]
    public Task<Response<List<BetaGroupDto>>> GetGroupsAsync([FromQuery] GetBetaGroupListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the testers.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("testers")]
    public Task<ListResponse<BetaTesterDto>> GetTestersAsync(GetBetaTesterListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The beta app texts per language.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("localizations")]
    public Task<Response<List<BetaLocalizationDto>>> GetLocalizationsAsync([FromQuery] GetBetaLocalizationListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the TestFlight state now. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "testflight-sync")]
    public Task<Response<long>> SyncAsync(SyncTestFlightCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Creates a beta group. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("group")]
    [Idempotent]
    public Task<Response<long>> CreateGroupAsync(CreateBetaGroupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes a beta group (name, public link). Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("group")]
    public Task<Response<long>> UpdateGroupAsync(UpdateBetaGroupCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Invites and removes testers of a group. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("group/testers")]
    public Task<Response<long>> UpdateGroupTestersAsync(UpdateBetaGroupTestersCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds and removes builds of a group. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("group/builds")]
    public Task<Response<long>> UpdateGroupBuildsAsync(UpdateBetaGroupBuildsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Writes the "what to test" text of a build. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("build/what-to-test")]
    public Task<Response<long>> UpdateWhatToTestAsync(UpdateWhatToTestCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Writes the beta app texts of a language. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("localizations")]
    public Task<Response<long>> UpdateLocalizationAsync(UpdateBetaLocalizationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Submits a build to beta app review (confirmation required). Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("build/beta-review")]
    [Idempotent]
    [RateLimit(20, 3600, "testflight-beta-review")]
    public Task<Response<long>> SubmitBetaReviewAsync(SubmitBetaReviewCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
