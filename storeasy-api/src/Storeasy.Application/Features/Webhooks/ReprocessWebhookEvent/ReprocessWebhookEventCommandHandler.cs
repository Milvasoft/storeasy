using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.ReprocessWebhookEvent;

/// <summary>
/// Handles <see cref="ReprocessWebhookEventCommand"/>.
/// </summary>
/// <param name="EventRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record ReprocessWebhookEventCommandHandler(IStoreasyRepositoryBase<WebhookEvent> EventRepository,
                                                  IStoreJobQueue StoreJobQueue,
                                                  IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<ReprocessWebhookEventCommand, long>
{
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = EventRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(ReprocessWebhookEventCommand request, CancellationToken cancellationToken)
    {
        var webhookEvent = await _eventRepository.GetFirstOrDefaultAsync(e => e.Id == request.EventId && e.OrganizationId == request.OrganizationId,
                                                                         projection: e => new { e.Id, e.SignatureValid, e.EventType },
                                                                         cancellationToken: cancellationToken);

        if (webhookEvent == null)
            return Response<long>.Error(0, MessageKey.WebhookEventNotFound);

        if (!webhookEvent.SignatureValid || webhookEvent.EventType != AppStoreVersionStates.StateUpdatedEvent)
            return Response<long>.Error(0, MessageKey.WebhookEventNotReprocessable);

        await _eventRepository.ExecuteUpdateAsync(webhookEvent.Id,
                                                  new SetPropertyBuilder<WebhookEvent>().SetPropertyValue(e => e.ProcessResult, WebhookProcessResult.Pending)
                                                                                        .SetPropertyValue(e => e.ProcessedDate, (DateTime?)null),
                                                  cancellationToken);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.ProcessWebhookEvent, new ProcessWebhookEventPayload(webhookEvent.Id), StoreKind.Apple)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.WebhookEventReprocessed, webhookEvent.Id.ToString(), $"Webhook event {webhookEvent.Id} queued for processing again.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
