using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.PingWebhookEndpoint;

/// <summary>
/// Handles <see cref="PingWebhookEndpointCommand"/>.
/// </summary>
/// <param name="EndpointRepository"></param>
/// <param name="StoreJobQueue"></param>
public record PingWebhookEndpointCommandHandler(IStoreasyRepositoryBase<WebhookEndpoint> EndpointRepository,
                                                IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<PingWebhookEndpointCommand, long>
{
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = EndpointRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(PingWebhookEndpointCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == request.EndpointId && e.OrganizationId == request.OrganizationId,
                                                                        projection: e => new { e.Id, e.Source, e.StoreConnectionId, e.ExternalWebhooks },
                                                                        cancellationToken: cancellationToken);

        if (endpoint == null)
            return Response<long>.Error(0, MessageKey.WebhookEndpointNotFound);

        if (endpoint.Source != WebhookSource.AppStoreConnect || !endpoint.StoreConnectionId.HasValue || (endpoint.ExternalWebhooks?.Count ?? 0) == 0)
            return Response<long>.Error(0, MessageKey.WebhookEndpointNotPingable);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.PingWebhooks, new PingWebhooksPayload(endpoint.Id, endpoint.StoreConnectionId.Value), StoreKind.Apple)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
