namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One webhook the store keeps for an inbound endpoint. App Store Connect webhooks belong to an app, so an endpoint of a
/// connection has one registration per app of that connection, all pointing at the same address with the same secret.
/// </summary>
public class ExternalWebhookRegistration
{
    /// <summary>
    /// The store's id of the app the webhook belongs to.
    /// </summary>
    public string AppExternalId { get; set; }

    /// <summary>
    /// The store's id of the webhook.
    /// </summary>
    public string WebhookId { get; set; }
}
