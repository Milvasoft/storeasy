using Microsoft.EntityFrameworkCore;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using System.Globalization;
using System.Text.Json;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Default <see cref="IWebhookInbox"/>. Replay protection: an event id is accepted once per endpoint (unique index), an
/// App Store Connect event older than <see cref="WebhookOptions.MaxEventAgeHours"/> and a signed event whose timestamp is
/// further than <see cref="WebhookOptions.SignedToleranceSeconds"/> from now are kept as stale and not processed. Events
/// with an invalid signature or a stale timestamp are kept without an event id, so they can never block a real one.
/// </summary>
/// <param name="endpointRepository"></param>
/// <param name="eventRepository"></param>
/// <param name="secretProtector"></param>
/// <param name="storeJobQueue"></param>
/// <param name="auditTrail"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="activityPublisher"></param>
/// <param name="googlePushTokenValidator"></param>
/// <param name="options"></param>
/// <param name="appOptions"></param>
/// <param name="timeProvider"></param>
public sealed class WebhookInbox(IStoreasyRepositoryBase<WebhookEndpoint> endpointRepository,
                                 IStoreasyRepositoryBase<WebhookEvent> eventRepository,
                                 ISecretProtector secretProtector,
                                 IStoreJobQueue storeJobQueue,
                                 IAuditTrail auditTrail,
                                 INotificationDispatcher notificationDispatcher,
                                 IActivityPublisher activityPublisher,
                                 IGooglePushTokenValidator googlePushTokenValidator,
                                 WebhookOptions options,
                                 AppOptions appOptions,
                                 TimeProvider timeProvider) : IWebhookInbox
{
    private readonly IGooglePushTokenValidator _googlePushTokenValidator = googlePushTokenValidator;
    private readonly AppOptions _appOptions = appOptions;
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = endpointRepository;
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = eventRepository;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly WebhookOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<WebhookReceipt> ReceiveAsync(InboundWebhookRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublicId) || request.PublicId.Length > WebhookEndpoint.PublicIdMaxLength)
            return new WebhookReceipt(WebhookReceiptOutcome.NoEndpoint, null);

        var endpoint = await _endpointRepository.GetFirstOrDefaultAsync(e => e.PublicId == request.PublicId && e.Source == request.Source,
                                                                        projection: e => new WebhookEndpoint
                                                                        {
                                                                            Id = e.Id,
                                                                            OrganizationId = e.OrganizationId,
                                                                            PublicId = e.PublicId,
                                                                            Name = e.Name,
                                                                            Source = e.Source,
                                                                            StoreConnectionId = e.StoreConnectionId,
                                                                            Registration = e.Registration,
                                                                            EncryptedSecret = e.EncryptedSecret,
                                                                            EncryptedDataKey = e.EncryptedDataKey,
                                                                            KeyVersion = e.KeyVersion,
                                                                            PreviousEncryptedSecret = e.PreviousEncryptedSecret,
                                                                            PreviousEncryptedDataKey = e.PreviousEncryptedDataKey,
                                                                            PreviousKeyVersion = e.PreviousKeyVersion,
                                                                            PreviousSecretValidUntil = e.PreviousSecretValidUntil,
                                                                            PushServiceAccount = e.PushServiceAccount,
                                                                        },
                                                                        cancellationToken: cancellationToken);

        if (endpoint == null)
            return new WebhookReceipt(WebhookReceiptOutcome.NoEndpoint, null);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var parsed = Parse(request);
        var secrets = WebhookEndpointSecrets.Valid(_secretProtector, endpoint, now);

        var valid = request.Source switch
        {
            WebhookSource.AppStoreConnect => secrets.Any(secret => WebhookSignatures.VerifyApple(request.Body, request.Signature, secret)),
            WebhookSource.GooglePlay => await _googlePushTokenValidator.ValidateAsync(request.Signature, _options.ReceiverUrl(_appOptions, endpoint.Source, endpoint.PublicId), endpoint.PushServiceAccount, cancellationToken),
            _ => parsed.Timestamp.HasValue && secrets.Any(secret => WebhookSignatures.VerifyStoreasy(parsed.Timestamp.Value, request.Body, request.Signature, secret)),
        };

        var webhookEvent = new WebhookEvent
        {
            OrganizationId = endpoint.OrganizationId,
            WebhookEndpointId = endpoint.Id,
            EventType = Trim(parsed.EventType),
            SignatureHeaderMasked = Trim(request.Source == WebhookSource.GooglePlay ? MaskBearer(request.Signature) : WebhookSignatures.Mask(request.Signature)),
            SignatureValid = valid,
            ReceivedDate = now,
        };

        if (!valid)
        {
            await RejectAsync(endpoint, webhookEvent, WebhookProcessResult.InvalidSignature, "The signature did not match.", now, cancellationToken);

            return new WebhookReceipt(WebhookReceiptOutcome.InvalidSignature, webhookEvent.Id);
        }

        if (IsStale(request.Source, parsed, now))
        {
            await RejectAsync(endpoint, webhookEvent, WebhookProcessResult.Stale, "The event timestamp is outside the accepted window.", now, cancellationToken);

            return new WebhookReceipt(WebhookReceiptOutcome.Stale, webhookEvent.Id);
        }

        webhookEvent.ExternalEventId = Trim(parsed.EventId ?? WebhookSignatures.Sha256(request.Body));

        var earlier = await _eventRepository.GetFirstOrDefaultAsync(e => e.WebhookEndpointId == endpoint.Id && e.ExternalEventId == webhookEvent.ExternalEventId,
                                                                    projection: e => (long?)e.Id,
                                                                    cancellationToken: cancellationToken);

        if (earlier.HasValue)
            return new WebhookReceipt(WebhookReceiptOutcome.Duplicate, earlier);

        var isStateEvent = request.Source == WebhookSource.AppStoreConnect && webhookEvent.EventType == AppStoreVersionStates.StateUpdatedEvent && parsed.SubjectId != null;

        var logged = request.Source is WebhookSource.Signed or WebhookSource.GooglePlay;

        webhookEvent.Payload = parsed.IsJson ? parsed.StoredPayload ?? request.Body : null;
        webhookEvent.SubjectExternalId = Trim(parsed.SubjectId);
        webhookEvent.OldValue = Trim(parsed.OldValue);
        webhookEvent.NewValue = Trim(parsed.NewValue);
        webhookEvent.ProcessResult = isStateEvent ? WebhookProcessResult.Pending : logged ? WebhookProcessResult.Processed : WebhookProcessResult.Ignored;
        webhookEvent.ProcessedDate = isStateEvent ? null : now;
        webhookEvent.ProcessMessage = isStateEvent ? null : logged ? "Logged." : "Nothing to do for this event type.";
        webhookEvent.RelatedEntityType = NotificationEntity.WebhookEvent;

        try
        {
            await _eventRepository.AddAsync(webhookEvent, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same event arrived twice at once; the other request stored it.
            return new WebhookReceipt(WebhookReceiptOutcome.Duplicate, null);
        }

        var touch = new SetPropertyBuilder<WebhookEndpoint>().SetPropertyValue(e => e.LastEventDate, (DateTime?)now);

        if (endpoint.Registration is WebhookRegistrationState.Pending or WebhookRegistrationState.Failed)
            touch.SetPropertyValue(e => e.Registration, WebhookRegistrationState.Manual);

        await _endpointRepository.ExecuteUpdateAsync(endpoint.Id, touch, cancellationToken);

        if (isStateEvent)
        {
            _auditTrail.Channel = ActionChannel.System;

            await _storeJobQueue.EnqueueAsync([new StoreJobRequest(endpoint.OrganizationId, StoreJobKind.ProcessWebhookEvent, new ProcessWebhookEventPayload(webhookEvent.Id), StoreKind.Apple)], cancellationToken);
        }

        await _activityPublisher.PublishAsync(new ActivityEventDto
        {
            Type = ActivityEventTypes.WebhookReceived,
            OrganizationId = endpoint.OrganizationId,
            Store = request.Source switch
            {
                WebhookSource.AppStoreConnect => StoreKind.Apple,
                WebhookSource.GooglePlay => StoreKind.Google,
                _ => null,
            },
            RelatedEntityType = NotificationEntity.WebhookEvent,
            RelatedEntityId = webhookEvent.Id.ToString(CultureInfo.InvariantCulture),
            Message = webhookEvent.NewValue == null ? webhookEvent.EventType : $"{webhookEvent.OldValue} -> {webhookEvent.NewValue}",
            Date = now,
        }, cancellationToken);

        return new WebhookReceipt(WebhookReceiptOutcome.Accepted, webhookEvent.Id);
    }

    private async Task RejectAsync(WebhookEndpoint endpoint, WebhookEvent webhookEvent, WebhookProcessResult result, string message, DateTime now, CancellationToken cancellationToken)
    {
        var alertSince = now.AddHours(-1);

        var alerted = result == WebhookProcessResult.InvalidSignature
                      && await _eventRepository.GetFirstOrDefaultAsync(e => e.WebhookEndpointId == endpoint.Id && e.ProcessResult == WebhookProcessResult.InvalidSignature && e.ReceivedDate > alertSince,
                                                                       projection: e => (long?)e.Id,
                                                                       cancellationToken: cancellationToken) != null;

        webhookEvent.ProcessResult = result;
        webhookEvent.ProcessedDate = now;
        webhookEvent.ProcessMessage = message;

        await _eventRepository.AddAsync(webhookEvent, cancellationToken);

        if (result != WebhookProcessResult.InvalidSignature || alerted)
            return;

        await _notificationDispatcher.DispatchAsync(new OrgNotification
        {
            OrganizationId = endpoint.OrganizationId,
            Kind = NotificationType.ConnectionAlert,
            Roles = NotificationAudiences.OwnersAndAdmins,
            TextKey = MessageKey.NotificationWebhookSignatureInvalid,
            TextArgs = [endpoint.Name],
            Data = new { endpointId = endpoint.Id, eventId = webhookEvent.Id },
            RelatedEntity = NotificationEntity.WebhookEvent,
            RelatedEntityId = webhookEvent.Id.ToString(CultureInfo.InvariantCulture),
            ActionLink = $"/webhooks?event={webhookEvent.Id}",
        }, cancellationToken);
    }

    private bool IsStale(WebhookSource source, ParsedEvent parsed, DateTime now)
    {
        if (source == WebhookSource.Signed)
            return !parsed.Timestamp.HasValue || Math.Abs(now.Subtract(DateTimeOffset.FromUnixTimeSeconds(parsed.Timestamp.Value).UtcDateTime).TotalSeconds) > Math.Max(1, _options.SignedToleranceSeconds);

        return parsed.EventDate.HasValue && now - parsed.EventDate.Value > TimeSpan.FromHours(Math.Max(1, _options.MaxEventAgeHours));
    }

    private static ParsedEvent Parse(InboundWebhookRequest request)
    {
        long? timestamp = long.TryParse(request.Timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) ? seconds : null;

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;

            if (request.Source == WebhookSource.Signed)
            {
                return new ParsedEvent(true, Text(root, "type") ?? "signed", request.EventId ?? Text(root, "id"), Text(root, "subject"), null, null, null, timestamp);
            }

            if (request.Source == WebhookSource.GooglePlay)
            {
                // The raw push carries the purchase token in base64; only the decoded notification with the tokens masked is kept.
                var play = PlayDeveloperNotifications.Parse(request.Body);

                return play == null
                    ? new ParsedEvent(false, "invalid", null, null, null, null, null, null)
                    : new ParsedEvent(true, play.EventType, play.MessageId, play.PackageName, null, play.ProductId, play.EventDate, null, play.RedactedJson);
            }

            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : default;
            var attributes = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("attributes", out var a) ? a : default;
            var instance = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("relationships", out var r) && r.TryGetProperty("instance", out var i) ? i : default;
            var instanceData = instance.ValueKind == JsonValueKind.Object && instance.TryGetProperty("data", out var id) ? id : instance;
            var eventType = AppStoreVersionStates.NormalizeEventType(Text(data, "type") ?? Text(root, "eventType") ?? "unknown");
            DateTime? eventDate = DateTimeOffset.TryParse(Text(attributes, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date.UtcDateTime : null;

            return new ParsedEvent(true, eventType, Text(data, "id"), Text(instanceData, "id"), Text(attributes, "oldValue"), Text(attributes, "newValue"), eventDate, timestamp);
        }
        catch (JsonException)
        {
            return new ParsedEvent(false, "invalid", request.EventId, null, null, null, null, timestamp);
        }
    }

    private static string Text(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Trim(string value) => value?.Length > WebhookEvent.TextMaxLength ? value[..WebhookEvent.TextMaxLength] : value;

    private static string MaskBearer(string authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
            return null;

        var value = authorization.Trim();
        var token = value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : value;

        return token.Length <= 8 ? "Bearer ********" : $"Bearer {token[..4]}***{token[^4..]}";
    }

    private sealed record ParsedEvent(bool IsJson, string EventType, string EventId, string SubjectId, string OldValue, string NewValue, DateTime? EventDate, long? Timestamp, string StoredPayload = null);
}
