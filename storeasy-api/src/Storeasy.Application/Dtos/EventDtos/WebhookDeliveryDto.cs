using Storeasy.Domain.Notifications;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// One outgoing delivery in the delivery log.
/// </summary>
public class WebhookDeliveryDto
{
    /// <summary>Delivery id.</summary>
    public long Id { get; set; }

    /// <summary>Subscription.</summary>
    public int OrganizationWebhookId { get; set; }

    /// <summary>Event type.</summary>
    public string EventType { get; set; }

    /// <summary>Event id (same for every attempt).</summary>
    public string EventId { get; set; }

    /// <summary>State.</summary>
    public WebhookDeliveryStatus Status { get; set; }

    /// <summary>Attempts made.</summary>
    public int Attempt { get; set; }

    /// <summary>Attempts allowed.</summary>
    public int MaxAttempts { get; set; }

    /// <summary>UTC instant of the next attempt.</summary>
    public DateTime? NextAttemptDate { get; set; }

    /// <summary>HTTP status of the last answer.</summary>
    public int? ResponseStatusCode { get; set; }

    /// <summary>Start of the last answer body.</summary>
    public string ResponseBody { get; set; }

    /// <summary>Reason of the last failure.</summary>
    public string Error { get; set; }

    /// <summary>Duration of the last attempt.</summary>
    public int? DurationMilliseconds { get; set; }

    /// <summary>Test delivery.</summary>
    public bool IsTest { get; set; }

    /// <summary>UTC instant queued.</summary>
    public DateTime CreationDate { get; set; }

    /// <summary>UTC instant of the last attempt.</summary>
    public DateTime? LastAttemptDate { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<WebhookDelivery, WebhookDeliveryDto>> Projection { get; } = d => new WebhookDeliveryDto
    {
        Id = d.Id,
        OrganizationWebhookId = d.OrganizationWebhookId,
        EventType = d.EventType,
        EventId = d.EventId,
        Status = d.Status,
        Attempt = d.Attempt,
        MaxAttempts = d.MaxAttempts,
        NextAttemptDate = d.NextAttemptDate,
        ResponseStatusCode = d.ResponseStatusCode,
        ResponseBody = d.ResponseBody,
        Error = d.Error,
        DurationMilliseconds = d.DurationMilliseconds,
        IsTest = d.IsTest,
        CreationDate = d.CreationDate,
        LastAttemptDate = d.LastAttemptDate,
    };

    /// <summary>
    /// Maps a delivery read in memory.
    /// </summary>
    public static WebhookDeliveryDto From(WebhookDelivery delivery) => Projection.Compile()(delivery);
}
