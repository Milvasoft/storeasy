using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Listings.GetReviewInfo;
using Storeasy.Application.Features.Listings.PlanReviewInfo;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App Review details of an App Store version, compared with the app's saved answers. Passwords are write only.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/review-info")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ReviewInfoController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the review details live.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<ReviewInfoDto>> GetReviewInfoAsync([FromQuery] GetReviewInfoQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans the review details (a demo password is sealed into the saved answers, never into the plan).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanReviewInfoAsync(PlanReviewInfoCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
