using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// The <see cref="StoreJobKind.PingWebhooks"/> job: one App Store Connect ping per webhook of the endpoint. App Store
/// Connect then posts a signed <c>webhookPingCreated</c> event to the receiver, which logs it like any other event.
/// </summary>
/// <param name="endpointRepository"></param>
/// <param name="gateway"></param>
public sealed class PingWebhooksJobHandler(IStoreasyRepositoryBase<WebhookEndpoint> endpointRepository, IStoreEventGateway gateway) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = endpointRepository;
    private readonly IStoreEventGateway _gateway = gateway;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.PingWebhooks;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<PingWebhooksPayload>() ?? throw new StoreJobException("The job has no endpoint.", transient: false);
        var connection = await context.OpenConnectionAsync(payload.ConnectionId, cancellationToken) ?? throw new StoreJobException("The store connection was removed.", transient: false);

        var webhooks = await _endpointRepository.GetFirstOrDefaultAsync(e => e.Id == payload.EndpointId && e.OrganizationId == context.OrganizationId,
                                                                        projection: e => e.ExternalWebhooks,
                                                                        cancellationToken: cancellationToken) ?? throw new StoreJobException("The endpoint was removed.", transient: false);

        var step = await context.BeginStepAsync("webhooks.ping", StepDirection.Outbound, total: webhooks.Count, cancellationToken: cancellationToken);

        for (var i = 0; i < webhooks.Count; i++)
        {
            await _gateway.PingWebhookAsync(connection.SecretJson, webhooks[i].WebhookId, cancellationToken);

            await context.ReportProgressAsync(step, i + 1, cancellationToken);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{webhooks.Count} pings.", cancellationToken);

        return new StoreJobOutcome($"{webhooks.Count} App Store Connect webhook pings sent.", new { Pinged = webhooks.Count });
    }
}
