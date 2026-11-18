using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Features.ArtifactSources.ReceiveArtifactWebhook;
using Storeasy.Application.Features.ArtifactSources.ReceiveEasWebhook;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using System.Text;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Anonymous receivers of the pull sources (design 13.19, 13.20): <c>/api/webhooks/eas/{sourceId}</c> and
/// <c>/api/webhooks/artifacts/{sourceId}</c> (the web container's proxy passes <c>/api</c>, like the store webhooks), also
/// answered at <c>/webhooks/...</c> as the design names them. The signature is checked on the raw body, which is limited
/// to 1 MiB; an accepted package answers 202 with its session.
/// </summary>
[ApiController]
[Route(GlobalConstant.RoutePrefix + "/webhooks")]
[ApiVersionNeutral]
[AllowAnonymous]
[ApiExplorerSettings(GroupName = "v1.0")]
public class ArtifactWebhooksController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// EAS build webhook (expo-signature, HMAC-SHA1 of the raw body).
    /// </summary>
    /// <param name="sourceId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("eas/{sourceId}")]
    [HttpPost("/webhooks/eas/{sourceId}")]
    [RequestSizeLimit(PullWebhooks.MaxBodyBytes)]
    [RateLimit(120, 60, "webhook-eas")]
    public async Task<Response<ArtifactUploadDto>> ReceiveEasAsync([FromRoute] string sourceId, CancellationToken cancellation)
    {
        var response = await _mediator.Send(new ReceiveEasWebhookCommand
        {
            SourceId = sourceId,
            Signature = Request.Headers["expo-signature"].ToString(),
            RawBody = await ReadBodyAsync(cancellation),
        }, cancellation);

        return WithAcceptedStatus(response);
    }

    /// <summary>
    /// Generic signed webhook (X-Storeasy-Timestamp, X-Storeasy-Signature: HMAC-SHA256 of "timestamp.body").
    /// </summary>
    /// <param name="sourceId"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("artifacts/{sourceId}")]
    [HttpPost("/webhooks/artifacts/{sourceId}")]
    [RequestSizeLimit(PullWebhooks.MaxBodyBytes)]
    [RateLimit(120, 60, "webhook-artifacts")]
    public async Task<Response<ArtifactUploadDto>> ReceiveArtifactAsync([FromRoute] string sourceId, CancellationToken cancellation)
    {
        var response = await _mediator.Send(new ReceiveArtifactWebhookCommand
        {
            SourceId = sourceId,
            Signature = Request.Headers["X-Storeasy-Signature"].ToString(),
            Timestamp = Request.Headers["X-Storeasy-Timestamp"].ToString(),
            RawBody = await ReadBodyAsync(cancellation),
        }, cancellation);

        return WithAcceptedStatus(response);
    }

    private async Task<string> ReadBodyAsync(CancellationToken cancellation)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);

        var body = await reader.ReadToEndAsync(cancellation);

        return body.Length > PullWebhooks.MaxBodyBytes ? body[..PullWebhooks.MaxBodyBytes] : body;
    }

    private Response<ArtifactUploadDto> WithAcceptedStatus(Response<ArtifactUploadDto> response)
    {
        if (response.IsSuccess && response.Data is { Duplicate: false, SessionId: > 0 })
            Response.StatusCode = StatusCodes.Status202Accepted;

        return response;
    }
}
