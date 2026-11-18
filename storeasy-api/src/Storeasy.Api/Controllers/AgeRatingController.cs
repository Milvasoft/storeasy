using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Listings.GetAgeRating;
using Storeasy.Application.Features.Listings.PlanAgeRating;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App Store age rating declaration; the new descriptors the API lacks are assisted steps.
/// </summary>
[ApiController]
[Route(GlobalConstant.RouteBase + "/age-rating")]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class AgeRatingController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the age rating declaration live.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<AgeRatingDto>> GetAgeRatingAsync([FromQuery] GetAgeRatingQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans age rating answers (DryRun false approves at once).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanAgeRatingAsync(PlanAgeRatingCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
