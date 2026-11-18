using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Features.Webhooks.CreateOrgWebhook;
using Storeasy.Application.Features.Webhooks.CreateWebhookEndpoint;
using Storeasy.Application.Features.Webhooks.DeleteOrgWebhook;
using Storeasy.Application.Features.Webhooks.DeleteWebhookEndpoint;
using Storeasy.Application.Features.Webhooks.GetOrgWebhookList;
using Storeasy.Application.Features.Webhooks.GetWebhookDeliveryList;
using Storeasy.Application.Features.Webhooks.GetWebhookEndpointList;
using Storeasy.Application.Features.Webhooks.GetWebhookEventDetail;
using Storeasy.Application.Features.Webhooks.GetWebhookEventList;
using Storeasy.Application.Features.Webhooks.ReprocessWebhookEvent;
using Storeasy.Application.Features.Webhooks.RotateOrgWebhookSecret;
using Storeasy.Application.Features.Webhooks.RotateWebhookSecret;
using Storeasy.Application.Features.Webhooks.TestOrgWebhook;
using Storeasy.Application.Features.Webhooks.UpdateOrgWebhook;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Webhook management (owners and admins): inbound endpoints and their event log, outgoing subscriptions and their
/// delivery log.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class WebhooksController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the inbound endpoints with their addresses.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet]
    public Task<Response<List<WebhookEndpointDto>>> GetEndpointsAsync([FromQuery] GetWebhookEndpointListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Adds an inbound endpoint (App Store Connect: registered at Apple by a job). The secret is returned once.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("endpoint")]
    [Idempotent]
    public Task<Response<WebhookSecretDto>> CreateEndpointAsync(CreateWebhookEndpointCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Rotates the secret of an inbound endpoint (the old one stays valid for 24 hours). The secret is returned once.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("endpoint/rotate-secret")]
    [RateLimit(20, 3600, "webhook-rotate")]
    public Task<Response<WebhookSecretDto>> RotateEndpointSecretAsync(RotateWebhookSecretCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes an inbound endpoint (its App Store Connect webhooks are deleted by a job).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("endpoint")]
    public Task<Response<int>> DeleteEndpointAsync(DeleteWebhookEndpointCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the inbound event log.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("events")]
    public Task<ListResponse<WebhookEventDto>> GetEventsAsync(GetWebhookEventListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads one inbound event with its raw body.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("event")]
    public Task<Response<WebhookEventDetailDto>> GetEventAsync([FromQuery] GetWebhookEventDetailQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Processes an App Store Connect event again. Returns the job id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("event/reprocess")]
    [RateLimit(60, 3600, "webhook-reprocess")]
    public Task<Response<long>> ReprocessEventAsync(ReprocessWebhookEventCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the outgoing subscriptions.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("subscriptions")]
    public Task<Response<List<OrgWebhookDto>>> GetSubscriptionsAsync([FromQuery] GetOrgWebhookListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Subscribes an HTTPS address (public addresses only). The signing secret is returned once.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("subscription")]
    [Idempotent]
    public Task<Response<WebhookSecretDto>> CreateSubscriptionAsync(CreateOrgWebhookCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes a subscription.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("subscription")]
    public Task<Response<int>> UpdateSubscriptionAsync(UpdateOrgWebhookCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a subscription.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("subscription")]
    public Task<Response<int>> DeleteSubscriptionAsync(DeleteOrgWebhookCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Rotates the signing secret of a subscription. The secret is returned once.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("subscription/rotate-secret")]
    [RateLimit(20, 3600, "webhook-rotate")]
    public Task<Response<WebhookSecretDto>> RotateSubscriptionSecretAsync(RotateOrgWebhookSecretCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Sends a signed test event now and returns the attempt.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("subscription/test")]
    [RateLimit(30, 3600, "webhook-test")]
    public Task<Response<WebhookDeliveryDto>> TestSubscriptionAsync(TestOrgWebhookCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the delivery log of the outgoing webhooks.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("deliveries")]
    public Task<ListResponse<WebhookDeliveryDto>> GetDeliveriesAsync(GetWebhookDeliveryListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
