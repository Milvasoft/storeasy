using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.ArtifactSources.ReceiveArtifactWebhook;

/// <summary>
/// A generic signed "artifact ready" webhook (design 13.20, schema storeasy.artifact-ready/v1): <c>X-Storeasy-Signature:
/// sha256=hex</c> is HMAC-SHA256 of <c>X-Storeasy-Timestamp.raw body</c>, timestamps older than 5 minutes are refused, and
/// <c>sha256</c> is required (the downloaded file must match it).
/// </summary>
public record ReceiveArtifactWebhookCommand : ICommand<ArtifactUploadDto>
{
    /// <summary>
    /// Public id of the source (from the URL).
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// The <c>X-Storeasy-Signature</c> header.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// The <c>X-Storeasy-Timestamp</c> header (unix seconds).
    /// </summary>
    public string Timestamp { get; set; }

    /// <summary>
    /// The raw request body.
    /// </summary>
    public string RawBody { get; set; }
}
