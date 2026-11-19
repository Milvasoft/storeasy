using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using Storeasy.Domain.JsonModels;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// The <see cref="StoreJobKind.RegisterWebhooks"/> job. App Store Connect webhooks belong to an app, so an endpoint of a
/// connection gets one webhook per App Store record of that connection, all with the endpoint's address and secret.
/// A key without the right role fails the registration (the endpoint shows the manual steps then).
/// </summary>
/// <param name="endpointRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="secretProtector"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="webhookOptions"></param>
/// <param name="appOptions"></param>
public sealed class RegisterWebhooksJobHandler(IStoreasyRepositoryBase<WebhookEndpoint> endpointRepository,
                                               IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                               IStoreEventGateway gateway,
                                               ISecretProtector secretProtector,
                                               INotificationDispatcher notificationDispatcher,
                                               WebhookOptions webhookOptions,
                                               AppOptions appOptions) : IStoreJobHandler
{
    private const string _webhookName = "storeasy";

    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = endpointRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreEventGateway _gateway = gateway;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly WebhookOptions _webhookOptions = webhookOptions;
    private readonly AppOptions _appOptions = appOptions;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.RegisterWebhooks;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<RegisterWebhooksPayload>() ?? throw new StoreJobException("The job has no endpoint.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken);

        if (payload.Remove)
        {
            if (connection == null)
                return new StoreJobOutcome("The connection was removed; App Store Connect keeps its webhooks until they are deleted there.");

            var removeStep = await context.BeginStepAsync("webhooks.delete", StepDirection.Outbound, total: payload.WebhookIds?.Count ?? 0, cancellationToken: cancellationToken);

            foreach (var webhookId in payload.WebhookIds ?? [])
                await _gateway.DeleteWebhookAsync(connection.SecretJson, webhookId, cancellationToken);

            await context.EndStepAsync(removeStep, StoreJobStepStatus.Succeeded, cancellationToken: cancellationToken);

            return new StoreJobOutcome($"{payload.WebhookIds?.Count ?? 0} App Store Connect webhooks deleted.");
        }

        if (connection == null)
            throw new StoreJobException("The store connection was removed.", transient: false);

        var endpoint = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == payload.EndpointId && e.OrganizationId == context.OrganizationId,
                                                                        projection: e => new WebhookEndpoint { Id = e.Id, OrganizationId = e.OrganizationId, PublicId = e.PublicId, Name = e.Name, Source = e.Source, Events = e.Events, ExternalWebhooks = e.ExternalWebhooks, EncryptedSecret = e.EncryptedSecret, EncryptedDataKey = e.EncryptedDataKey, KeyVersion = e.KeyVersion },
                                                                        cancellationToken: cancellationToken) ?? throw new StoreJobException("The endpoint was removed.", transient: false);

        var secret = WebhookEndpointSecrets.Current(_secretProtector, endpoint) ?? throw new StoreJobException("The endpoint secret could not be opened.", transient: false);

        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == context.OrganizationId && s.Store == StoreKind.Apple && s.StoreConnectionId == connection.Id,
                                                            projection: s => new StoreApp { Id = s.Id, ExternalId = s.ExternalId },
                                                            cancellationToken: cancellationToken);

        var registration = new StoreWebhookRegistration(_webhookName, _webhookOptions.ReceiverUrl(_appOptions, endpoint.Source, endpoint.PublicId), secret, endpoint.Events?.Count > 0 ? endpoint.Events : AppStoreVersionStates.RegisteredEvents);
        var registered = new List<ExternalWebhookRegistration>();
        var step = await context.BeginStepAsync("webhooks.register", StepDirection.Outbound, total: records.Count, cancellationToken: cancellationToken);

        try
        {
            for (var i = 0; i < records.Count; i++)
            {
                var existing = endpoint.ExternalWebhooks?.FirstOrDefault(w => w.AppExternalId == records[i].ExternalId)?.WebhookId;
                var webhookId = await _gateway.UpsertWebhookAsync(connection.SecretJson, records[i].ExternalId, existing, registration, cancellationToken);

                registered.Add(new ExternalWebhookRegistration { AppExternalId = records[i].ExternalId, WebhookId = webhookId });

                await context.ReportProgressAsync(step, i + 1, cancellationToken);
            }
        }
        catch (StoreCallException ex) when (!ex.Transient)
        {
            await SaveAsync(endpoint.Id, registered.Count > 0 ? registered : endpoint.ExternalWebhooks, WebhookRegistrationState.Failed, ex.Message, cancellationToken);

            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                OrganizationId = context.OrganizationId,
                Kind = NotificationType.ConnectionAlert,
                Roles = NotificationAudiences.OwnersAndAdmins,
                TextKey = MessageKey.NotificationWebhookRegistrationFailed,
                TextArgs = [endpoint.Name, ex.Message],
                RelatedEntity = NotificationEntity.StoreConnection,
                RelatedEntityId = connection.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ActionLink = "/webhooks",
            }, cancellationToken);

            throw;
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{registered.Count} webhooks.", cancellationToken);

        await SaveAsync(endpoint.Id, registered, WebhookRegistrationState.Api, null, cancellationToken);

        return new StoreJobOutcome($"{registered.Count} App Store Connect webhooks registered.", new { Registered = registered.Count });
    }

    private Task SaveAsync(int endpointId, List<ExternalWebhookRegistration> webhooks, WebhookRegistrationState state, string error, CancellationToken cancellationToken)
        => _endpointRepository.ExecuteUpdateAsync(endpointId,
                                                  new SetPropertyBuilder<WebhookEndpoint>().SetPropertyValue(e => e.ExternalWebhooks, webhooks ?? [])
                                                                                           .SetPropertyValue(e => e.Registration, state)
                                                                                           .SetPropertyValue(e => e.RegistrationError, error?.Length > 500 ? error[..500] : error),
                                                  cancellationToken);
}
