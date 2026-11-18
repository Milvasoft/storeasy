using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Webhooks.ReceiveWebhook;

/// <summary>
/// An inbound webhook request (anonymous; the HMAC signature is the authentication). Returns the stored event id.
/// </summary>
public record ReceiveWebhookCommand : ICommand<long>
{
    /// <summary>
    /// Signature scheme of the route the request came to.
    /// </summary>
    public WebhookSource Source { get; set; }

    /// <summary>
    /// Public id from the address.
    /// </summary>
    public string PublicId { get; set; }

    /// <summary>
    /// Raw body.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Signature header (<c>x-apple-signature</c> or <c>X-Storeasy-Signature</c>). Masked in logs.
    /// </summary>
    [LogMasked]
    public string Signature { get; set; }

    /// <summary>
    /// <c>X-Storeasy-Timestamp</c> (storeasy scheme).
    /// </summary>
    public string Timestamp { get; set; }

    /// <summary>
    /// <c>X-Storeasy-Event-Id</c> (storeasy scheme).
    /// </summary>
    public string EventId { get; set; }
}
