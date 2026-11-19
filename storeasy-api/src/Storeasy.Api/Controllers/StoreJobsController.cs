using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Application.Features.StoreJobs.CancelStoreJob;
using Storeasy.Application.Features.StoreJobs.GetActivityBoard;
using Storeasy.Application.Features.StoreJobs.GetStoreJobDetail;
using Storeasy.Application.Features.StoreJobs.GetStoreJobList;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Store job endpoints: the organization's queue of store work, its live state and its replayable steps. Members follow
/// the same jobs live on the activity hub (<c>/hubs/activity</c>).
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class StoreJobsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Pages through the organization's store jobs, newest first (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<StoreJobListDto>> GetJobsAsync(GetStoreJobListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one store job with its steps (any member).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("job")]
    public Task<Response<StoreJobDetailDto>> GetJobAsync([FromQuery] GetStoreJobDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// The live activity page's board (any member): versions with App Review, builds processing or close to their TestFlight
    /// expiry, and the last 24 hours of finished jobs and store events per store.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("board")]
    public Task<Response<ActivityBoardDto>> GetBoardAsync([FromQuery] GetActivityBoardQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Cancels a queued job at once, or asks a running job to stop at its next step (editors and above). Returns the state
    /// after the request.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("job/cancel")]
    public Task<Response<StoreJobStatus>> CancelJobAsync(CancelStoreJobCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
