using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEventList;

/// <summary>
/// Handles <see cref="GetWebhookEventListQuery"/>.
/// </summary>
/// <param name="EventRepository"></param>
public record GetWebhookEventListQueryHandler(IStoreasyRepositoryBase<WebhookEvent> EventRepository) : IInterceptable, IListQueryHandler<GetWebhookEventListQuery, WebhookEventDto>
{
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = EventRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<WebhookEventDto>> Handle(GetWebhookEventListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(WebhookEventDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        return await _eventRepository.GetAllAsync(request,
                                                  condition: e => e.OrganizationId == request.OrganizationId,
                                                  projection: WebhookEventDto.Projection,
                                                  cancellationToken: cancellationToken);
    }
}
