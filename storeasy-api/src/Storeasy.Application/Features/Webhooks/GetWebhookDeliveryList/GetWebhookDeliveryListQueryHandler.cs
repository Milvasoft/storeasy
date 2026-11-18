using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.GetWebhookDeliveryList;

/// <summary>
/// Handles <see cref="GetWebhookDeliveryListQuery"/>.
/// </summary>
/// <param name="DeliveryRepository"></param>
public record GetWebhookDeliveryListQueryHandler(IStoreasyRepositoryBase<WebhookDelivery> DeliveryRepository) : IInterceptable, IListQueryHandler<GetWebhookDeliveryListQuery, WebhookDeliveryDto>
{
    private readonly IStoreasyRepositoryBase<WebhookDelivery> _deliveryRepository = DeliveryRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<WebhookDeliveryDto>> Handle(GetWebhookDeliveryListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(WebhookDeliveryDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        return await _deliveryRepository.GetAllAsync(request,
                                                     condition: d => d.OrganizationId == request.OrganizationId && (request.WebhookId == null || d.OrganizationWebhookId == request.WebhookId),
                                                     projection: WebhookDeliveryDto.Projection,
                                                     cancellationToken: cancellationToken);
    }
}
