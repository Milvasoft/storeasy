using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using System.Globalization;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// The <see cref="StoreJobKind.ProcessWebhookEvent"/> job: reads the App Store version the event names (the event carries
/// only its id), matches it to a store record and applies the new state (<see cref="IVersionStateTracker"/>).
/// </summary>
/// <param name="eventRepository"></param>
/// <param name="endpointRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="tracker"></param>
/// <param name="timeProvider"></param>
public sealed class ProcessWebhookEventJobHandler(IStoreasyRepositoryBase<WebhookEvent> eventRepository,
                                                  IStoreasyRepositoryBase<WebhookEndpoint> endpointRepository,
                                                  IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                                  IStoreEventGateway gateway,
                                                  IVersionStateTracker tracker,
                                                  TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = eventRepository;
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = endpointRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreEventGateway _gateway = gateway;
    private readonly IVersionStateTracker _tracker = tracker;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.ProcessWebhookEvent;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<ProcessWebhookEventPayload>() ?? throw new StoreJobException("The job has no event.", transient: false);

        var webhookEvent = await _eventRepository.GetFirstOrDefaultAsync(e => e.Id == payload.EventId && e.OrganizationId == context.OrganizationId,
                                                                         projection: e => new WebhookEvent { Id = e.Id, WebhookEndpointId = e.WebhookEndpointId, SubjectExternalId = e.SubjectExternalId, NewValue = e.NewValue, ReceivedDate = e.ReceivedDate, SignatureValid = e.SignatureValid },
                                                                         cancellationToken: cancellationToken) ?? throw new StoreJobException("The event was removed.", transient: false);

        if (!webhookEvent.SignatureValid || webhookEvent.SubjectExternalId == null)
            return await FinishAsync(webhookEvent.Id, WebhookProcessResult.Ignored, "Nothing to process.", null, null, null, cancellationToken);

        var connectionId = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == webhookEvent.WebhookEndpointId,
                                                                            projection: e => e.StoreConnectionId,
                                                                            cancellationToken: cancellationToken);

        var connection = connectionId.HasValue ? await context.OpenConnectionAsync(connectionId.Value, cancellationToken) : null;

        if (connection == null)
            return await FinishAsync(webhookEvent.Id, WebhookProcessResult.Failed, "The endpoint or its store connection was removed.", null, null, null, cancellationToken);

        var step = await context.BeginStepAsync("version.read", StepDirection.Outbound, webhookEvent.SubjectExternalId, cancellationToken: cancellationToken);

        StoreVersionRecord version;

        try
        {
            version = await _gateway.GetVersionAsync(connection.SecretJson, webhookEvent.SubjectExternalId, cancellationToken);
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
            await FinishAsync(webhookEvent.Id, WebhookProcessResult.Failed, ex.Message, null, null, null, cancellationToken);
            throw;
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{version.VersionString} ({version.State})", cancellationToken);

        var storeApp = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == context.OrganizationId && s.Store == StoreKind.Apple && s.ExternalId == version.AppExternalId,
                                                                        projection: s => new StoreApp { Id = s.Id, AppId = s.AppId },
                                                                        cancellationToken: cancellationToken);

        if (storeApp == null)
            return await FinishAsync(webhookEvent.Id, WebhookProcessResult.Ignored, $"App {version.AppExternalId} is not in storeasy.", null, null, null, cancellationToken);

        var applyStep = await context.BeginStepAsync("state.apply", StepDirection.Internal, cancellationToken: cancellationToken);

        var outcomes = await _tracker.ApplyAsync(context.OrganizationId,
                                                 [new VersionStateChange(storeApp.Id, version.Id, version.VersionString, version.BuildNumber, webhookEvent.NewValue ?? version.State, webhookEvent.ReceivedDate)],
                                                 RejectionSource.Webhook,
                                                 cancellationToken);

        var outcome = outcomes.FirstOrDefault();
        var message = outcome?.OpenedCaseId != null ? "Rejection case opened."
                    : outcome?.ClosedCaseIds.Count > 0 ? $"{outcome.ClosedCaseIds.Count} rejection cases closed."
                    : outcome?.Changed == true ? "State updated."
                    : "State already known.";

        await context.EndStepAsync(applyStep, StoreJobStepStatus.Succeeded, message, cancellationToken);

        return outcome?.OpenedCaseId != null
            ? await FinishAsync(webhookEvent.Id, WebhookProcessResult.Processed, message, storeApp, NotificationEntity.RejectionCase, outcome.OpenedCaseId.Value.ToString(CultureInfo.InvariantCulture), cancellationToken)
            : await FinishAsync(webhookEvent.Id, WebhookProcessResult.Processed, message, storeApp, NotificationEntity.AppVersionState, version.Id, cancellationToken);
    }

    private async Task<StoreJobOutcome> FinishAsync(long eventId, WebhookProcessResult result, string message, StoreApp storeApp, NotificationEntity? relatedType, string relatedId, CancellationToken cancellationToken)
    {
        var update = new SetPropertyBuilder<WebhookEvent>().SetPropertyValue(e => e.ProcessResult, result)
                                                           .SetPropertyValue(e => e.ProcessedDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime)
                                                           .SetPropertyValue(e => e.ProcessMessage, message?.Length > 500 ? message[..500] : message);

        if (storeApp != null)
        {
            update.SetPropertyValue(e => e.StoreAppId, (int?)storeApp.Id)
                  .SetPropertyValue(e => e.AppId, (int?)storeApp.AppId);
        }

        if (relatedType.HasValue)
        {
            update.SetPropertyValue(e => e.RelatedEntityType, relatedType.Value)
                  .SetPropertyValue(e => e.RelatedEntityId, relatedId);
        }

        await _eventRepository.ExecuteUpdateAsync(eventId, update, cancellationToken);

        return new StoreJobOutcome(message, new { EventId = eventId, Result = result.ToString() });
    }
}
