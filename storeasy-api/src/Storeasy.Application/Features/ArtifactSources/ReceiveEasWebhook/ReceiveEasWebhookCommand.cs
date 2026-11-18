using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.IntakeDtos;

namespace Storeasy.Application.Features.ArtifactSources.ReceiveEasWebhook;

/// <summary>
/// An EAS build webhook (design 13.19): the <c>expo-signature</c> header is HMAC-SHA1 of the raw body with the source's
/// secret. A finished build of an accepted platform whose <c>artifacts.buildUrl</c> is on an allowed domain becomes an
/// upload session that storeasy downloads; other events are acknowledged and ignored.
/// </summary>
public record ReceiveEasWebhookCommand : ICommand<ArtifactUploadDto>
{
    /// <summary>
    /// Public id of the source (from the URL).
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// The <c>expo-signature</c> header.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// The raw request body.
    /// </summary>
    public string RawBody { get; set; }
}
