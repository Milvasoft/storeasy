using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Application.Features.Subscriptions.ApplySubscriptionPlan;
using Storeasy.Application.Features.Subscriptions.ConvertSubscriptionPrices;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionDetail;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionGroups;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionOffers;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionPricePoints;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionPrices;
using Storeasy.Application.Features.Subscriptions.PlanSubscriptionChanges;
using Storeasy.Application.Features.Subscriptions.SyncStoreProducts;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Subscriptions: App Store subscription groups, subscriptions, prices and offers; Play subscriptions, base plans and
/// offers. Reads show the last sync; every write is a change plan.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class SubscriptionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the subscription groups (App Store) or subscriptions (Play) of a store record as last synced. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("groups")]
    public Task<Response<SubscriptionGroupsDto>> GetGroupsAsync([FromQuery] GetSubscriptionGroupsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads a subscription as last synced. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("subscription")]
    public Task<Response<ProductDetailDto>> GetSubscriptionAsync([FromQuery] GetSubscriptionDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the territory prices of a subscription (one Play base plan with PlanId), searched, filtered and paged. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("prices")]
    public Task<ListResponse<ProductPriceDto>> GetPricesAsync(GetSubscriptionPricesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the App Store price points of a subscription in one territory, live. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("price-points")]
    [RateLimit(600, 3600, "store-read")]
    public Task<Response<List<PricePointDto>>> GetPricePointsAsync([FromQuery] GetSubscriptionPricePointsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the offers of a subscription by kind. Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("offers")]
    public Task<Response<SubscriptionOffersDto>> GetOffersAsync([FromQuery] GetSubscriptionOffersQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Converts a price to every Play region with Play's conversion (nothing is saved). Members (View).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("prices/convert")]
    [RateLimit(240, 3600, "store-read")]
    public Task<Response<ConvertedPricesDto>> ConvertPricesAsync(ConvertSubscriptionPricesQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Plans subscription changes (dry run by default; DryRun false approves at once). Editor and above; a price change of
    /// a product on sale needs ReleaseProduction (Owner, Admin) and, when it reaches existing subscribers, the typed
    /// confirmation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("plan")]
    [RateLimit(240, 3600, "content-plan")]
    public Task<Response<ChangePlanDto>> PlanAsync(PlanSubscriptionChangesCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Approves a subscription or In-App Purchase plan and queues its store job. The plan's own permission and typed
    /// confirmation are checked. Retry safe with an Idempotency-Key header.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apply")]
    [Idempotent]
    public Task<Response<ChangePlanDto>> ApplyAsync(ApplySubscriptionPlanCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Queues a sync of the subscriptions and In-App Purchases of a store record; returns the job id. Editor and above.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("sync")]
    [RateLimit(60, 3600, "store-sync")]
    public Task<Response<long>> SyncAsync(SyncStoreProductsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
