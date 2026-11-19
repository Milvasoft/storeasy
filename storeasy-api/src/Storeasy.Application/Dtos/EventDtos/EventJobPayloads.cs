namespace Storeasy.Application.Dtos.EventDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.ProcessWebhookEvent"/> job.
/// </summary>
/// <param name="EventId">Webhook event.</param>
public sealed record ProcessWebhookEventPayload(long EventId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.RegisterWebhooks"/> job.
/// </summary>
/// <param name="EndpointId">Inbound endpoint.</param>
/// <param name="ConnectionId">Its store connection.</param>
/// <param name="Remove">Removes the store's webhooks instead (the endpoint is being deleted).</param>
/// <param name="WebhookIds">The store's webhook ids to remove.</param>
public sealed record RegisterWebhooksPayload(int EndpointId, int ConnectionId, bool Remove = false, List<string> WebhookIds = null);

/// <summary>
/// Payload of a <see cref="StoreJobKind.PingWebhooks"/> job.
/// </summary>
/// <param name="EndpointId">Inbound endpoint.</param>
/// <param name="ConnectionId">Its store connection.</param>
public sealed record PingWebhooksPayload(int EndpointId, int ConnectionId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncVersionStates"/> job.
/// </summary>
/// <param name="ConnectionId">Store connection.</param>
public sealed record SyncVersionStatesPayload(int ConnectionId);
