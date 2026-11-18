using Storeasy.Domain.Events;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// An inbound webhook endpoint. Never the secret.
/// </summary>
public class WebhookEndpointDto
{
    /// <summary>Endpoint id.</summary>
    public int Id { get; set; }

    /// <summary>Store connection (App Store Connect endpoints).</summary>
    public int? StoreConnectionId { get; set; }

    /// <summary>Name of the connection.</summary>
    public string ConnectionName { get; set; }

    /// <summary>Sender.</summary>
    public WebhookSource Source { get; set; }

    /// <summary>Display name.</summary>
    public string Name { get; set; }

    /// <summary>Public id in the address.</summary>
    public string PublicId { get; set; }

    /// <summary>The address to register at the sender.</summary>
    public string Url { get; set; }

    /// <summary>Registration state.</summary>
    public WebhookRegistrationState Registration { get; set; }

    /// <summary>Reason of a failed registration.</summary>
    public string RegistrationError { get; set; }

    /// <summary>Webhooks the store keeps for the endpoint (one per app).</summary>
    public int ExternalWebhookCount { get; set; }

    /// <summary>The store's webhooks as read (the count is computed from them).</summary>
    [JsonIgnore]
    public List<ExternalWebhookRegistration> ExternalWebhooks { get; set; }

    /// <summary>Event types.</summary>
    public List<string> Events { get; set; }

    /// <summary>UTC instant the current secret was set.</summary>
    public DateTime SecretSetDate { get; set; }

    /// <summary>UTC instant until which the previous secret is accepted.</summary>
    public DateTime? PreviousSecretValidUntil { get; set; }

    /// <summary>UTC instant of the last valid event.</summary>
    public DateTime? LastEventDate { get; set; }

    /// <summary>UTC instant created.</summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// Projection from the entity (the address is filled by the handler).
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<WebhookEndpoint, WebhookEndpointDto>> Projection { get; } = e => new WebhookEndpointDto
    {
        Id = e.Id,
        StoreConnectionId = e.StoreConnectionId,
        ConnectionName = e.StoreConnection != null ? e.StoreConnection.Name : null,
        Source = e.Source,
        Name = e.Name,
        PublicId = e.PublicId,
        Registration = e.Registration,
        RegistrationError = e.RegistrationError,
        ExternalWebhooks = e.ExternalWebhooks,
        Events = e.Events,
        SecretSetDate = e.SecretSetDate,
        PreviousSecretValidUntil = e.PreviousSecretValidUntil,
        LastEventDate = e.LastEventDate,
        CreationDate = e.CreationDate,
    };

    /// <summary>
    /// Fills the computed fields.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public WebhookEndpointDto Complete(string url)
    {
        Url = url;
        ExternalWebhookCount = ExternalWebhooks?.Count ?? 0;

        return this;
    }
}
