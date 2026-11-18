using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Notifications;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// Default <see cref="IOrgWebhookPublisher"/>: one delivery row per matching active subscription, due now.
/// </summary>
/// <param name="webhookRepository"></param>
/// <param name="deliveryRepository"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class OrgWebhookPublisher(IStoreasyRepositoryBase<OrganizationWebhook> webhookRepository,
                                        IStoreasyRepositoryBase<WebhookDelivery> deliveryRepository,
                                        OrgWebhookOptions options,
                                        TimeProvider timeProvider) : IOrgWebhookPublisher
{
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = webhookRepository;
    private readonly IStoreasyRepositoryBase<WebhookDelivery> _deliveryRepository = deliveryRepository;
    private readonly OrgWebhookOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<int> EnqueueAsync(int organizationId, string eventType, object data, CancellationToken cancellationToken)
    {
        var subscriptions = await _webhookRepository.GetAllAsync(w => w.OrganizationId == organizationId && w.IsActive,
                                                                 projection: w => new OrganizationWebhook { Id = w.Id, Events = w.Events },
                                                                 cancellationToken: cancellationToken);

        var matching = subscriptions.Where(w => w.Events == null || w.Events.Count == 0 || w.Events.Contains(eventType)).ToList();

        if (matching.Count == 0)
            return 0;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = OrgWebhookPayloads.Envelope(eventId, eventType, organizationId, now, data);

        var deliveries = matching.Select(w => new WebhookDelivery
        {
            OrganizationId = organizationId,
            OrganizationWebhookId = w.Id,
            EventType = eventType,
            EventId = eventId,
            Payload = payload,
            Status = WebhookDeliveryStatus.Pending,
            MaxAttempts = Math.Max(1, _options.MaxAttempts),
            NextAttemptDate = now,
            CreationDate = now,
        }).ToList();

        await _deliveryRepository.AddRangeAsync(deliveries, cancellationToken);

        return deliveries.Count;
    }
}
