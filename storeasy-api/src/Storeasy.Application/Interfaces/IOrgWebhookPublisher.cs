namespace Storeasy.Application.Interfaces;

/// <summary>
/// Queues an event for the organization's outgoing webhooks that subscribe to it. The delivery runner posts it, signed,
/// with retries; every attempt is kept in the delivery log.
/// </summary>
public interface IOrgWebhookPublisher
{
    /// <summary>
    /// Queues one delivery per active subscription of <paramref name="organizationId"/> that wants <paramref name="eventType"/>.
    /// Returns the number of deliveries queued.
    /// </summary>
    /// <param name="organizationId"></param>
    /// <param name="eventType">One of <see cref="Utils.Constants.OrgWebhookEventTypes"/>.</param>
    /// <param name="data">Event data, serialized to JSON. Must not carry a secret.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> EnqueueAsync(int organizationId, string eventType, object data, CancellationToken cancellationToken);
}
