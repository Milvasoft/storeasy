namespace Storeasy.Application.Interfaces;

/// <summary>
/// Receives inbound webhook requests (anonymous): finds the endpoint by its public id, checks the HMAC signature against
/// the current secret (and the previous one during a rotation), rejects stale and repeated events (replay protection),
/// stores the event as received and hands App Store version state events to a processing job. An invalid signature is
/// stored (without the body) and the endpoint's admins are alerted at most once an hour.
/// </summary>
public interface IWebhookInbox
{
    /// <summary>
    /// Handles one request.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<WebhookReceipt> ReceiveAsync(InboundWebhookRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// An inbound webhook request as it arrived.
/// </summary>
/// <param name="Source">Signature scheme of the route.</param>
/// <param name="PublicId">Public id from the address.</param>
/// <param name="Body">Raw body.</param>
/// <param name="Signature">Signature header.</param>
/// <param name="Timestamp">Timestamp header (storeasy scheme).</param>
/// <param name="EventId">Event id header (storeasy scheme).</param>
public sealed record InboundWebhookRequest(WebhookSource Source, string PublicId, string Body, string Signature, string Timestamp, string EventId);

/// <summary>
/// What happened to an inbound request.
/// </summary>
/// <param name="Outcome">Verdict.</param>
/// <param name="EventId">Stored event (or the earlier one for a repeat).</param>
public sealed record WebhookReceipt(WebhookReceiptOutcome Outcome, long? EventId);

/// <summary>
/// Verdict of an inbound request.
/// </summary>
public enum WebhookReceiptOutcome
{
    /// <summary>Stored (and queued for processing).</summary>
    Accepted = 1,

    /// <summary>The event was received before; nothing was done again.</summary>
    Duplicate = 2,

    /// <summary>No endpoint with that public id.</summary>
    NoEndpoint = 3,

    /// <summary>The signature did not match.</summary>
    InvalidSignature = 4,

    /// <summary>The event is too old (or its timestamp too far off).</summary>
    Stale = 5,
}
