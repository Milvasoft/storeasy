using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Events;
using Storeasy.Domain.Stores;
using System.Text;

namespace Storeasy.Application.Features.Webhooks.CreateWebhookEndpoint;

/// <summary>
/// Handles <see cref="CreateWebhookEndpointCommand"/>. No [Log]: the response carries the secret.
/// </summary>
/// <param name="EndpointRepository"></param>
/// <param name="ConnectionRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="WebhookOptions"></param>
/// <param name="AppOptions"></param>
/// <param name="TimeProvider"></param>
public record CreateWebhookEndpointCommandHandler(IStoreasyRepositoryBase<WebhookEndpoint> EndpointRepository,
                                                  IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                                  ISecretProtector SecretProtector,
                                                  IStoreJobQueue StoreJobQueue,
                                                  IAuditTrail AuditTrail,
                                                  WebhookOptions WebhookOptions,
                                                  AppOptions AppOptions,
                                                  TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreateWebhookEndpointCommand, WebhookSecretDto>
{
    private const string _publicIdPrefix = "whe_";
    private const string _secretPrefix = "whsec_";

    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = EndpointRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly WebhookOptions _webhookOptions = WebhookOptions;
    private readonly AppOptions _appOptions = AppOptions;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<WebhookSecretDto>> Handle(CreateWebhookEndpointCommand request, CancellationToken cancellationToken)
    {
        string name = request.Name?.Trim();

        if (request.Source == WebhookSource.AppStoreConnect)
        {
            var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.StoreConnectionId && c.OrganizationId == request.OrganizationId,
                                                                                projection: c => new { c.Id, c.Store, c.Name },
                                                                                cancellationToken: cancellationToken);

            if (connection == null)
                return Response<WebhookSecretDto>.Error(null, MessageKey.StoreConnectionNotFound);

            if (connection.Store != StoreKind.Apple)
                return Response<WebhookSecretDto>.Error(null, MessageKey.StoreConnectionStoreMismatch);

            var taken = await _endpointRepository.GetFirstOrDefaultAsync(e => e.OrganizationId == request.OrganizationId && e.StoreConnectionId == connection.Id,
                                                                         projection: e => (int?)e.Id,
                                                                         cancellationToken: cancellationToken);

            if (taken.HasValue)
                return Response<WebhookSecretDto>.Error(null, MessageKey.WebhookEndpointExists);

            name = string.IsNullOrEmpty(name) ? connection.Name : name;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var publicId = SecureTokens.Create(_publicIdPrefix)[..40];
        var secret = SecureTokens.Create(_secretPrefix);
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), SecretScopes.WebhookEndpoint(request.OrganizationId, publicId));

        var endpoint = new WebhookEndpoint
        {
            OrganizationId = request.OrganizationId,
            StoreConnectionId = request.Source == WebhookSource.AppStoreConnect ? request.StoreConnectionId : null,
            Source = request.Source,
            PublicId = publicId,
            Name = string.IsNullOrEmpty(name) ? request.Source.ToString() : name,
            EncryptedSecret = sealedSecret.EncryptedSecret,
            EncryptedDataKey = sealedSecret.EncryptedDataKey,
            KeyVersion = sealedSecret.KeyVersion,
            SecretSetDate = now,
            Registration = WebhookRegistrationState.Pending,
            Events = request.Source == WebhookSource.AppStoreConnect ? [AppStoreVersionStates.StateUpdatedEvent] : [],
        };

        await _endpointRepository.AddAsync(endpoint, cancellationToken);

        long? jobId = null;

        if (request.Source == WebhookSource.AppStoreConnect && request.RegisterAtStore)
        {
            var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.RegisterWebhooks, new RegisterWebhooksPayload(endpoint.Id, endpoint.StoreConnectionId.Value), StoreKind.Apple)], cancellationToken);

            jobId = jobIds[0];
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.WebhookEndpointAdded, endpoint.Id.ToString(), $"{request.Source} webhook endpoint '{endpoint.Name}' added.", request.Source == WebhookSource.AppStoreConnect ? StoreKind.Apple : null, cancellationToken: cancellationToken);

        return Response<WebhookSecretDto>.Success(new WebhookSecretDto
        {
            Id = endpoint.Id,
            Url = _webhookOptions.ReceiverUrl(_appOptions, endpoint.Source, publicId),
            Secret = secret,
            Registration = endpoint.Registration,
            SecretSetDate = now,
            JobId = jobId,
        });
    }
}
