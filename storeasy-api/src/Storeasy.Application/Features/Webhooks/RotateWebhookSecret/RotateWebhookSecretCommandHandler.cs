using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Events;
using System.Text;

namespace Storeasy.Application.Features.Webhooks.RotateWebhookSecret;

/// <summary>
/// Handles <see cref="RotateWebhookSecretCommand"/>. No [Log]: the response carries the secret.
/// </summary>
/// <param name="EndpointRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="WebhookOptions"></param>
/// <param name="AppOptions"></param>
/// <param name="TimeProvider"></param>
public record RotateWebhookSecretCommandHandler(IStoreasyRepositoryBase<WebhookEndpoint> EndpointRepository,
                                                ISecretProtector SecretProtector,
                                                IStoreJobQueue StoreJobQueue,
                                                IAuditTrail AuditTrail,
                                                WebhookOptions WebhookOptions,
                                                AppOptions AppOptions,
                                                TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RotateWebhookSecretCommand, WebhookSecretDto>
{
    private const string _secretPrefix = "whsec_";

    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = EndpointRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly WebhookOptions _webhookOptions = WebhookOptions;
    private readonly AppOptions _appOptions = AppOptions;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<WebhookSecretDto>> Handle(RotateWebhookSecretCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == request.EndpointId && e.OrganizationId == request.OrganizationId,
                                                                        projection: e => new { e.Id, e.PublicId, e.Source, e.StoreConnectionId, e.Registration, e.Name, e.EncryptedSecret, e.EncryptedDataKey, e.KeyVersion },
                                                                        cancellationToken: cancellationToken);

        if (endpoint == null)
            return Response<WebhookSecretDto>.Error(null, MessageKey.WebhookEndpointNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var validUntil = now.AddHours(Math.Max(0, _webhookOptions.SecretGraceHours));
        var secret = SecureTokens.Create(_secretPrefix);
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), SecretScopes.WebhookEndpoint(request.OrganizationId, endpoint.PublicId));

        var update = new SetPropertyBuilder<WebhookEndpoint>().SetPropertyValue(e => e.PreviousEncryptedSecret, endpoint.EncryptedSecret)
                                                              .SetPropertyValue(e => e.PreviousEncryptedDataKey, endpoint.EncryptedDataKey)
                                                              .SetPropertyValue(e => e.PreviousKeyVersion, (int?)endpoint.KeyVersion)
                                                              .SetPropertyValue(e => e.PreviousSecretValidUntil, (DateTime?)validUntil)
                                                              .SetPropertyValue(e => e.EncryptedSecret, sealedSecret.EncryptedSecret)
                                                              .SetPropertyValue(e => e.EncryptedDataKey, sealedSecret.EncryptedDataKey)
                                                              .SetPropertyValue(e => e.KeyVersion, sealedSecret.KeyVersion)
                                                              .SetPropertyValue(e => e.SecretSetDate, now);

        await _endpointRepository.ExecuteUpdateAsync(endpoint.Id, update, cancellationToken);

        long? jobId = null;

        if (endpoint.Source == WebhookSource.AppStoreConnect && endpoint.StoreConnectionId.HasValue && endpoint.Registration == WebhookRegistrationState.Api)
        {
            var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.RegisterWebhooks, new RegisterWebhooksPayload(endpoint.Id, endpoint.StoreConnectionId.Value), StoreKind.Apple)], cancellationToken);

            jobId = jobIds[0];
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.WebhookSecretRotated, endpoint.Id.ToString(), $"Secret of webhook endpoint '{endpoint.Name}' rotated.", cancellationToken: cancellationToken);

        return Response<WebhookSecretDto>.Success(new WebhookSecretDto
        {
            Id = endpoint.Id,
            Url = _webhookOptions.ReceiverUrl(_appOptions, endpoint.Source, endpoint.PublicId),
            Secret = secret,
            Registration = endpoint.Registration,
            SecretSetDate = now,
            PreviousSecretValidUntil = validUntil,
            JobId = jobId,
        });
    }
}
