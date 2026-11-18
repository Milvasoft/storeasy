using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.GetOrgWebhookList;

/// <summary>
/// Handles <see cref="GetOrgWebhookListQuery"/>.
/// </summary>
/// <param name="WebhookRepository"></param>
public record GetOrgWebhookListQueryHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository) : IInterceptable, IQueryHandler<GetOrgWebhookListQuery, List<OrgWebhookDto>>
{
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;

    /// <inheritdoc/>
    public async Task<Response<List<OrgWebhookDto>>> Handle(GetOrgWebhookListQuery request, CancellationToken cancellationToken)
    {
        var webhooks = await _webhookRepository.GetAllAsync(w => w.OrganizationId == request.OrganizationId,
                                                            projection: OrgWebhookDto.Projection,
                                                            cancellationToken: cancellationToken);

        return Response<List<OrgWebhookDto>>.Success([.. webhooks.OrderBy(w => w.Id)]);
    }
}
