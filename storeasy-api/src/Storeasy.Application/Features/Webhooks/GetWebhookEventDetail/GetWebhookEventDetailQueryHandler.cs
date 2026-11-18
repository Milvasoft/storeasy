using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEventDetail;

/// <summary>
/// Handles <see cref="GetWebhookEventDetailQuery"/>.
/// </summary>
/// <param name="EventRepository"></param>
public record GetWebhookEventDetailQueryHandler(IStoreasyRepositoryBase<WebhookEvent> EventRepository) : IInterceptable, IQueryHandler<GetWebhookEventDetailQuery, WebhookEventDetailDto>
{
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = EventRepository;

    /// <inheritdoc/>
    public async Task<Response<WebhookEventDetailDto>> Handle(GetWebhookEventDetailQuery request, CancellationToken cancellationToken)
    {
        var webhookEvent = await _eventRepository.GetFirstOrDefaultAsync(e => e.Id == request.EventId && e.OrganizationId == request.OrganizationId,
                                                                         projection: WebhookEventDetailDto.DetailProjection,
                                                                         cancellationToken: cancellationToken);

        if (webhookEvent == null)
            return Response<WebhookEventDetailDto>.Error(null, MessageKey.WebhookEventNotFound);

        return Response<WebhookEventDetailDto>.Success(webhookEvent);
    }
}
