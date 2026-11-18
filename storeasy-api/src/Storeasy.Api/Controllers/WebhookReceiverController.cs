using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Features.Webhooks.ReceiveWebhook;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Inbound webhook receivers (anonymous; the HMAC signature is the authentication), at <c>/api/webhooks/...</c> so the
/// web container's proxy passes them like any API call. Rate limited per sender address and body size limited.
/// </summary>
[ApiController]
[Route(GlobalConstant.RoutePrefix + "/webhooks")]
[ApiVersionNeutral]
[ApiExplorerSettings(GroupName = "v1.0")]
[AllowAnonymous]
public class WebhookReceiverController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// App Store Connect events (<c>x-apple-signature: hmacsha256=&lt;hex&gt;</c> over the raw body).
    /// </summary>
    /// <param name="publicId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("apple/{publicId}")]
    [RequestSizeLimit(WebhookOptions.MaxBodyBytes)]
    [RateLimit(600, 60, "webhook-receive")]
    public async Task<Response<long>> ReceiveAppleAsync(string publicId, CancellationToken cancellation)
        => await _mediator.Send(new ReceiveWebhookCommand
        {
            Source = WebhookSource.AppStoreConnect,
            PublicId = publicId,
            Body = await ReadBodyAsync(cancellation),
            Signature = Request.Headers[WebhookSignatures.AppleHeader].FirstOrDefault(),
        }, cancellation);

    /// <summary>
    /// Events of any other sender with storeasy's scheme: <c>X-Storeasy-Signature: sha256=&lt;hex&gt;</c> over
    /// <c>{timestamp}.{body}</c>, <c>X-Storeasy-Timestamp</c> (unix seconds, within 5 minutes) and <c>X-Storeasy-Event-Id</c>.
    /// </summary>
    /// <param name="publicId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("signed/{publicId}")]
    [RequestSizeLimit(WebhookOptions.MaxBodyBytes)]
    [RateLimit(600, 60, "webhook-receive")]
    public async Task<Response<long>> ReceiveSignedAsync(string publicId, CancellationToken cancellation)
        => await _mediator.Send(new ReceiveWebhookCommand
        {
            Source = WebhookSource.Signed,
            PublicId = publicId,
            Body = await ReadBodyAsync(cancellation),
            Signature = Request.Headers[WebhookSignatures.SignatureHeader].FirstOrDefault(),
            Timestamp = Request.Headers[WebhookSignatures.TimestampHeader].FirstOrDefault(),
            EventId = Request.Headers[WebhookSignatures.EventIdHeader].FirstOrDefault(),
        }, cancellation);

    private async Task<string> ReadBodyAsync(CancellationToken cancellation)
    {
        using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);

        return await reader.ReadToEndAsync(cancellation);
    }
}
