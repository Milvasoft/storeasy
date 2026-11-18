using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Events;

namespace Storeasy.Application.Features.Webhooks.GetWebhookEndpointList;

/// <summary>
/// Handles <see cref="GetWebhookEndpointListQuery"/>.
/// </summary>
/// <param name="EndpointRepository"></param>
/// <param name="WebhookOptions"></param>
/// <param name="AppOptions"></param>
public record GetWebhookEndpointListQueryHandler(IStoreasyRepositoryBase<WebhookEndpoint> EndpointRepository,
                                                 WebhookOptions WebhookOptions,
                                                 AppOptions AppOptions) : IInterceptable, IQueryHandler<GetWebhookEndpointListQuery, List<WebhookEndpointDto>>
{
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = EndpointRepository;
    private readonly WebhookOptions _webhookOptions = WebhookOptions;
    private readonly AppOptions _appOptions = AppOptions;

    /// <inheritdoc/>
    public async Task<Response<List<WebhookEndpointDto>>> Handle(GetWebhookEndpointListQuery request, CancellationToken cancellationToken)
    {
        var endpoints = await _endpointRepository.GetAllAsync(e => e.OrganizationId == request.OrganizationId,
                                                              projection: WebhookEndpointDto.Projection,
                                                              cancellationToken: cancellationToken);

        var ordered = endpoints.OrderBy(e => e.Id).ToList();

        ordered.ForEach(e => e.Complete(_webhookOptions.ReceiverUrl(_appOptions, e.Source, e.PublicId)));

        return Response<List<WebhookEndpointDto>>.Success(ordered);
    }
}
