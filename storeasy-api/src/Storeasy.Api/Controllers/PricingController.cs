using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.Listings.GetPricePoints;
using Storeasy.Application.Features.Listings.GetPricing;
using Storeasy.Application.Features.Listings.PlanPricing;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// App Store price schedule and territory availability.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class PricingController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Reads the price schedule and availability live.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<PricingDto>> GetPricingAsync([FromQuery] GetPricingQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the price points of a territory.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("price-points")]
    public Task<Response<List<StoreOptionDto>>> GetPricePointsAsync([FromQuery] GetPricePointsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans the base price and territory availability (DryRun false approves at once).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanPricingAsync(PlanPricingCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
