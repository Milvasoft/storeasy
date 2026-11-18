using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.ChangePlans.ApplyChangePlan;
using Storeasy.Application.Features.Listings.GetListing;
using Storeasy.Application.Features.Listings.GetListingByField;
using Storeasy.Application.Features.Listings.GetListingDetails;
using Storeasy.Application.Features.Listings.PlanListing;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Listing text endpoints: live reads of App Store and Play listings with the catalog limits, and change plans for them.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ListingsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the listing texts of a store record live, with the catalog limits.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<ListingDto>> GetListingAsync([FromQuery] GetListingQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one field in every locale.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("by-field")]
    public Task<Response<List<ListingFieldDto>>> GetListingByFieldAsync([FromQuery] GetListingByFieldQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the Google Play app details.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("details")]
    public Task<Response<ListingDetailsDto>> GetDetailsAsync([FromQuery] GetListingDetailsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans listing text changes (texts and Play app details) and returns the change plan.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("plan")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanListingAsync(PlanListingCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves a listing plan and queues its apply job.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apply")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApplyListingAsync(ApplyChangePlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
