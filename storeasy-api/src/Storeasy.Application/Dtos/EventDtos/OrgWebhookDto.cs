using Storeasy.Domain.Notifications;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// An outgoing webhook subscription. Never the secret.
/// </summary>
public class OrgWebhookDto
{
    /// <summary>Subscription id.</summary>
    public int Id { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    /// <summary>Address.</summary>
    public string Url { get; set; }

    /// <summary>Subscribed event types (empty: all).</summary>
    public List<string> Events { get; set; }

    /// <summary>Whether deliveries are made.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC instant of the last delivery attempt.</summary>
    public DateTime? LastDeliveryDate { get; set; }

    /// <summary>Result of the last delivery attempt.</summary>
    public WebhookDeliveryStatus? LastDeliveryStatus { get; set; }

    /// <summary>UTC instant created.</summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Projection from the entity.
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<OrganizationWebhook, OrgWebhookDto>> Projection { get; } = w => new OrgWebhookDto
    {
        Id = w.Id,
        Name = w.Name,
        Url = w.Url,
        Events = w.Events,
        IsActive = w.IsActive,
        LastDeliveryDate = w.LastDeliveryDate,
        LastDeliveryStatus = w.LastDeliveryStatus,
        CreationDate = w.CreationDate,
    };
}
