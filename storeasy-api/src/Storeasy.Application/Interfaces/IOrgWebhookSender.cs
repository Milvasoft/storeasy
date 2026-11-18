namespace Storeasy.Application.Interfaces;

/// <summary>
/// Makes delivery attempts of outgoing webhooks: checks the address against private networks again (the name may now
/// resolve elsewhere), signs the body (HMAC-SHA256 over <c>{timestamp}.{body}</c>), posts it without following redirects
/// and records the answer.
/// </summary>
public interface IOrgWebhookSender
{
    /// <summary>
    /// Claims the due deliveries (a short lease, so several instances never send one twice) and attempts each once.
    /// Returns the number attempted.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> SendDueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts one delivery right away (test deliveries) and returns it as stored afterwards.
    /// </summary>
    /// <param name="deliveryId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Domain.Notifications.WebhookDelivery> SendNowAsync(long deliveryId, CancellationToken cancellationToken);
}
