using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Notifications;

/// <summary>
/// One event to post to one outgoing webhook, with its attempts (the delivery log). Claimed by the delivery runner with a
/// short lease (<see cref="LockedUntil"/>), retried with backoff until <see cref="MaxAttempts"/>.
/// </summary>
[Table(TableNames.WebhookDeliveries)]
[Index(nameof(Status), nameof(NextAttemptDate))]
[Index(nameof(OrganizationWebhookId), nameof(CreationDate), IsDescending = [false, true])]
public class WebhookDelivery : BaseEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 100;

    /// <summary>
    /// Column length of <see cref="ResponseBody"/> and <see cref="Error"/>.
    /// </summary>
    public const int ResponseMaxLength = 1000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Subscription.
    /// </summary>
    public int OrganizationWebhookId { get; set; }

    /// <summary>
    /// Event type (<c>rejection.opened</c>...).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string EventType { get; set; }

    /// <summary>
    /// Id of the event, sent as <c>X-Storeasy-Event-Id</c>; the same for every attempt so receivers can deduplicate.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string EventId { get; set; }

    /// <summary>
    /// Body posted (JSON).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Payload { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    /// <summary>
    /// Attempts made.
    /// </summary>
    public int Attempt { get; set; }

    /// <summary>
    /// Attempts allowed.
    /// </summary>
    public int MaxAttempts { get; set; }

    /// <summary>
    /// UTC instant of the next attempt.
    /// </summary>
    public DateTime? NextAttemptDate { get; set; }

    /// <summary>
    /// Lease of the runner working on it.
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// HTTP status of the last answer.
    /// </summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>
    /// Start of the last answer body, redacted.
    /// </summary>
    [MaxLength(ResponseMaxLength)]
    public string ResponseBody { get; set; }

    /// <summary>
    /// Redacted reason of the last failure.
    /// </summary>
    [MaxLength(ResponseMaxLength)]
    public string Error { get; set; }

    /// <summary>
    /// Duration of the last attempt in milliseconds.
    /// </summary>
    public int? DurationMilliseconds { get; set; }

    /// <summary>
    /// Whether a member asked for it as a test.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// UTC instant it was queued.
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// UTC instant of the last attempt.
    /// </summary>
    public DateTime? LastAttemptDate { get; set; }

    /// <summary>
    /// Id of the claim that holds the lease (set and read back in one round, cleared after the attempt).
    /// </summary>
    [MaxLength(40)]
    public string LeaseId { get; set; }
}
