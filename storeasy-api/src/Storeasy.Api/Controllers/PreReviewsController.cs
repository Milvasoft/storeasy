using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Application.Features.PreReviews.GetPreReviewDetail;
using Storeasy.Application.Features.PreReviews.GetPreReviewList;
using Storeasy.Application.Features.PreReviews.StartPreReview;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// AI pre-reviews: Claude, with the organization's Anthropic API key, reviews the version being prepared the way the store's
/// reviewer would and estimates whether it passes. Manual only; a pre-review never blocks or performs a submission.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/pre-reviews")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class PreReviewsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// The pre-review history of one app in one store, newest first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<List<PreReviewListDto>>> GetPreReviewsAsync([FromQuery] GetPreReviewListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// One pre-review with its report.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("pre-review")]
    public Task<Response<PreReviewDto>> GetPreReviewAsync([FromQuery] GetPreReviewDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Starts a pre-review as a background job and returns it queued; the report arrives with the job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("pre-review")]
    [RateLimit(20, 3600, "pre-review-start")]
    public Task<Response<PreReviewDto>> StartPreReviewAsync(StartPreReviewCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
