using Storeasy.Domain.Events;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// An inbound webhook event in the event log.
/// </summary>
public class WebhookEventDto
{
    /// <summary>Event id.</summary>
    public long Id { get; set; }

    /// <summary>Endpoint.</summary>
    public int WebhookEndpointId { get; set; }

    /// <summary>Event type.</summary>
    public string EventType { get; set; }

    /// <summary>storeasy app it was matched to.</summary>
    public int? AppId { get; set; }

    /// <summary>Store record it was matched to.</summary>
    public int? StoreAppId { get; set; }

    /// <summary>Previous value.</summary>
    public string OldValue { get; set; }

    /// <summary>New value.</summary>
    public string NewValue { get; set; }

    /// <summary>Whether the signature was valid.</summary>
    public bool SignatureValid { get; set; }

    /// <summary>UTC instant received.</summary>
    public DateTime ReceivedDate { get; set; }

    /// <summary>UTC instant processed.</summary>
    public DateTime? ProcessedDate { get; set; }

    /// <summary>Outcome.</summary>
    public WebhookProcessResult ProcessResult { get; set; }

    /// <summary>Outcome note.</summary>
    public string ProcessMessage { get; set; }

    /// <summary>Kind of the record processing touched.</summary>
    public NotificationEntity RelatedEntityType { get; set; }

    /// <summary>Id of that record.</summary>
    public string RelatedEntityId { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<WebhookEvent, WebhookEventDto>> Projection { get; } = e => new WebhookEventDto
    {
        Id = e.Id,
        WebhookEndpointId = e.WebhookEndpointId,
        EventType = e.EventType,
        AppId = e.AppId,
        StoreAppId = e.StoreAppId,
        OldValue = e.OldValue,
        NewValue = e.NewValue,
        SignatureValid = e.SignatureValid,
        ReceivedDate = e.ReceivedDate,
        ProcessedDate = e.ProcessedDate,
        ProcessResult = e.ProcessResult,
        ProcessMessage = e.ProcessMessage,
        RelatedEntityType = e.RelatedEntityType,
        RelatedEntityId = e.RelatedEntityId,
    };
}
