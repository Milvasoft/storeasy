using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.DeleteWebhookEndpoint;

/// <summary>
/// Handles <see cref="DeleteWebhookEndpointCommand"/>. The secrets are wiped before the soft delete.
/// </summary>
/// <param name="EndpointRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteWebhookEndpointCommandHandler(IStoreasyRepositoryBase<WebhookEndpoint> EndpointRepository,
                                                  IStoreJobQueue StoreJobQueue,
                                                  IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteWebhookEndpointCommand, int>
{
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = EndpointRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteWebhookEndpointCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == request.EndpointId && e.OrganizationId == request.OrganizationId,
                                                                        projection: e => new { e.Id, e.Name, e.StoreConnectionId, e.ExternalWebhooks },
                                                                        cancellationToken: cancellationToken);

        if (endpoint == null)
            return Response<int>.Error(0, MessageKey.WebhookEndpointNotFound);

        var wipe = new SetPropertyBuilder<WebhookEndpoint>().SetPropertyValue(e => e.EncryptedSecret, [])
                                                            .SetPropertyValue(e => e.EncryptedDataKey, [])
                                                            .SetPropertyValue(e => e.PreviousEncryptedSecret, null)
                                                            .SetPropertyValue(e => e.PreviousEncryptedDataKey, null);

        await _endpointRepository.ExecuteUpdateAsync(endpoint.Id, wipe, cancellationToken);

        await _endpointRepository.ExecuteDeleteAsync(endpoint.Id, cancellationToken: cancellationToken);

        if (endpoint.StoreConnectionId.HasValue && endpoint.ExternalWebhooks?.Count > 0)
        {
            var payload = new RegisterWebhooksPayload(endpoint.Id, endpoint.StoreConnectionId.Value, true, [.. endpoint.ExternalWebhooks.Select(w => w.WebhookId)]);

            await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.RegisterWebhooks, payload, StoreKind.Apple)], cancellationToken);
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.WebhookEndpointRemoved, endpoint.Id.ToString(), $"Webhook endpoint '{endpoint.Name}' removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(endpoint.Id);
    }
}
