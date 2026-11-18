namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// A webhook secret, returned once (on creation and rotation) and never again.
/// </summary>
public class WebhookSecretDto
{
    /// <summary>Endpoint or subscription id.</summary>
    public int Id { get; set; }

    /// <summary>The address to register at the sender (inbound endpoints).</summary>
    public string Url { get; set; }

    /// <summary>The secret.</summary>
    public string Secret { get; set; }

    /// <summary>Registration state (inbound endpoints).</summary>
    public WebhookRegistrationState? Registration { get; set; }

    /// <summary>UTC instant the secret was set.</summary>
    public DateTime SecretSetDate { get; set; }

    /// <summary>UTC instant until which the previous secret is accepted (rotation).</summary>
    public DateTime? PreviousSecretValidUntil { get; set; }

    /// <summary>Job that registers the webhooks at the store, if one.</summary>
    public long? JobId { get; set; }
}
