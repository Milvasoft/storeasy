using Storeasy.Domain.Events;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// One inbound event with its raw body and the masked signature header.
/// </summary>
public class WebhookEventDetailDto : WebhookEventDto
{
    /// <summary>Raw body (JSON).</summary>
    public string Payload { get; set; }

    /// <summary>Signature header, masked.</summary>
    public string SignatureHeaderMasked { get; set; }

    /// <summary>The sender's event id.</summary>
    public string ExternalEventId { get; set; }

    /// <summary>The store's id of the subject (App Store version id).</summary>
    public string SubjectExternalId { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<WebhookEvent, WebhookEventDetailDto>> DetailProjection { get; } = e => new WebhookEventDetailDto
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
        Payload = e.Payload,
        SignatureHeaderMasked = e.SignatureHeaderMasked,
        ExternalEventId = e.ExternalEventId,
        SubjectExternalId = e.SubjectExternalId,
    };
}
