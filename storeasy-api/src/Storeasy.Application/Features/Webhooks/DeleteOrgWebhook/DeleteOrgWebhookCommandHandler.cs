using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.DeleteOrgWebhook;

/// <summary>
/// Handles <see cref="DeleteOrgWebhookCommand"/>.
/// </summary>
/// <param name="WebhookRepository"></param>
/// <param name="DeliveryRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteOrgWebhookCommandHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository,
                                             IStoreasyRepositoryBase<WebhookDelivery> DeliveryRepository,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteOrgWebhookCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;
    private readonly IStoreasyRepositoryBase<WebhookDelivery> _deliveryRepository = DeliveryRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteOrgWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _webhookRepository.GetFirstOrDefaultAsync(w => w.Id == request.WebhookId && w.OrganizationId == request.OrganizationId,
                                                                      projection: w => new { w.Id, w.Name },
                                                                      cancellationToken: cancellationToken);

        if (webhook == null)
            return Response<int>.Error(0, MessageKey.OrgWebhookNotFound);

        await _webhookRepository.ExecuteUpdateAsync(webhook.Id,
                                                    new SetPropertyBuilder<OrganizationWebhook>().SetPropertyValue(w => w.EncryptedSecret, [])
                                                                                                 .SetPropertyValue(w => w.EncryptedDataKey, [])
                                                                                                 .SetPropertyValue(w => w.IsActive, false),
                                                    cancellationToken);

        await _webhookRepository.ExecuteDeleteAsync(webhook.Id, cancellationToken: cancellationToken);

        await _deliveryRepository.ExecuteUpdateAsync(d => d.OrganizationWebhookId == webhook.Id && (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Retrying),
                                                     new SetPropertyBuilder<WebhookDelivery>().SetPropertyValue(d => d.Status, WebhookDeliveryStatus.Failed)
                                                                                              .SetPropertyValue(d => d.Error, "The subscription was removed.")
                                                                                              .SetPropertyValue(d => d.NextAttemptDate, (DateTime?)null),
                                                     cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrgWebhookRemoved, webhook.Id.ToString(), $"Outgoing webhook '{webhook.Name}' removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(webhook.Id);
    }
}
