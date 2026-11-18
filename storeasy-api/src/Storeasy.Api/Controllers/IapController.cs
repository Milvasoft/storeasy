using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Features.Iap.ApplyIapPlan;
using Storeasy.Application.Features.Iap.GetIapDetail;
using Storeasy.Application.Features.Iap.GetIapList;
using Storeasy.Application.Features.Iap.GetIapPricePoints;
using Storeasy.Application.Features.Iap.GetIapPrices;
using Storeasy.Application.Features.Iap.PlanIapChanges;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// In-App Purchases (App Store consumables, non-consumables, non-renewing subscriptions) and Play one-time products.
/// Reads show the last sync; every write is a change plan.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class IapController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the In-App Purchases or one-time products of a store record, filtered and paged. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch]
    public Task<ListResponse<IapListDto>> GetProductsAsync(GetIapListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads an In-App Purchase or one-time product as last synced. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("product")]
    public Task<Response<ProductDetailDto>> GetProductAsync([FromQuery] GetIapDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the territory prices of a product (one Play purchase option with PlanId), searched, filtered and paged. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("prices")]
    public Task<ListResponse<ProductPriceDto>> GetPricesAsync(GetIapPricesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the App Store price points of an In-App Purchase in one territory, live. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("price-points")]
    [RateLimit(600, 3600, "store-read")]
    public Task<Response<List<PricePointDto>>> GetPricePointsAsync([FromQuery] GetIapPricePointsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans In-App Purchase changes (dry run by default). Editor and above; a price change of a product on sale needs
    /// ReleaseProduction (Owner, Admin).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("plan")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanAsync(PlanIapChangesCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves an In-App Purchase or subscription plan and queues its store job. Retry safe with an Idempotency-Key header.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apply")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApplyAsync(ApplyIapPlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
