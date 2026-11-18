using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Events;

/// <summary>
/// One request that reached an inbound webhook endpoint, kept as received (raw payload) with the signature verdict and the
/// processing outcome. An event the sender repeats (same <see cref="ExternalEventId"/>) is stored once: the unique index is
/// the replay protection.
/// </summary>
[Table(TableNames.WebhookEvents)]
[Index(nameof(WebhookEndpointId), nameof(ExternalEventId), IsUnique = true)]
[Index(nameof(OrganizationId), nameof(ReceivedDate), IsDescending = [false, true])]
public class WebhookEvent : BaseEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Endpoint that received the event.
    /// </summary>
    public int WebhookEndpointId { get; set; }

    /// <summary>
    /// Event type as sent (normalized for the App Store version state event).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string EventType { get; set; }

    /// <summary>
    /// The sender's id of the event, or the SHA-256 of the body when the sender gives none.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalEventId { get; set; }

    /// <summary>
    /// Raw body (JSON). Null when the body was not JSON.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Payload { get; set; }

    /// <summary>
    /// Signature header with everything but the first and last 4 hex characters masked.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string SignatureHeaderMasked { get; set; }

    /// <summary>
    /// Whether the signature matched the current (or the previous, during a rotation) secret.
    /// </summary>
    public bool SignatureValid { get; set; }

    /// <summary>
    /// UTC instant the request arrived.
    /// </summary>
    public DateTime ReceivedDate { get; set; }

    /// <summary>
    /// UTC instant processing finished.
    /// </summary>
    public DateTime? ProcessedDate { get; set; }

    /// <summary>
    /// Outcome.
    /// </summary>
    public WebhookProcessResult ProcessResult { get; set; } = WebhookProcessResult.Pending;

    /// <summary>
    /// Short, redacted note about the outcome.
    /// </summary>
    [MaxLength(500)]
    public string ProcessMessage { get; set; }

    /// <summary>
    /// The store's id of the object the event is about (an App Store version id).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string SubjectExternalId { get; set; }

    /// <summary>
    /// Store record the event was matched to.
    /// </summary>
    public int? StoreAppId { get; set; }

    /// <summary>
    /// storeasy app the event was matched to.
    /// </summary>
    public int? AppId { get; set; }

    /// <summary>
    /// Previous value (state change events).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string OldValue { get; set; }

    /// <summary>
    /// New value (state change events).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string NewValue { get; set; }

    /// <summary>
    /// Kind of the record processing produced or touched.
    /// </summary>
    public NotificationEntity RelatedEntityType { get; set; }

    /// <summary>
    /// Id of that record.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string RelatedEntityId { get; set; }
}
