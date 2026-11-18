using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.TestOrgWebhook;

/// <summary>
/// Handles <see cref="TestOrgWebhookCommand"/>.
/// </summary>
/// <param name="WebhookRepository"></param>
/// <param name="DeliveryRepository"></param>
/// <param name="Sender"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record TestOrgWebhookCommandHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository,
                                           IStoreasyRepositoryBase<WebhookDelivery> DeliveryRepository,
                                           IOrgWebhookSender Sender,
                                           ICurrentUser CurrentUser,
                                           TimeProvider TimeProvider) : IInterceptable, ICommandHandler<TestOrgWebhookCommand, WebhookDeliveryDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;
    private readonly IStoreasyRepositoryBase<WebhookDelivery> _deliveryRepository = DeliveryRepository;
    private readonly IOrgWebhookSender _sender = Sender;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<WebhookDeliveryDto>> Handle(TestOrgWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _webhookRepository.GetFirstOrDefaultAsync(w => w.Id == request.WebhookId && w.OrganizationId == request.OrganizationId,
                                                                      projection: w => new { w.Id },
                                                                      cancellationToken: cancellationToken);

        if (webhook == null)
            return Response<WebhookDeliveryDto>.Error(null, MessageKey.OrgWebhookNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = OrgWebhookPayloads.Envelope(eventId, OrgWebhookEventTypes.Test, request.OrganizationId, now, new { message = "storeasy test delivery", requestedBy = _currentUser.UserName });

        var delivery = new WebhookDelivery
        {
            OrganizationId = request.OrganizationId,
            OrganizationWebhookId = webhook.Id,
            EventType = OrgWebhookEventTypes.Test,
            EventId = eventId,
            Payload = payload,
            Status = WebhookDeliveryStatus.Pending,
            MaxAttempts = 1,
            IsTest = true,
            CreationDate = now,
        };

        await _deliveryRepository.AddAsync(delivery, cancellationToken);

        var attempted = await _sender.SendNowAsync(delivery.Id, cancellationToken);

        return Response<WebhookDeliveryDto>.Success(WebhookDeliveryDto.From(attempted));
    }
}
