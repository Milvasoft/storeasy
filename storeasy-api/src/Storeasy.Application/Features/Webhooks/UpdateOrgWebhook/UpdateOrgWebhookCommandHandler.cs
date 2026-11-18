using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Notifications;

namespace Storeasy.Application.Features.Webhooks.UpdateOrgWebhook;

/// <summary>
/// Handles <see cref="UpdateOrgWebhookCommand"/>.
/// </summary>
/// <param name="WebhookRepository"></param>
/// <param name="UrlGuard"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateOrgWebhookCommandHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository,
                                             IWebhookUrlGuard UrlGuard,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateOrgWebhookCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;
    private readonly IWebhookUrlGuard _urlGuard = UrlGuard;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateOrgWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _webhookRepository.GetFirstOrDefaultAsync(w => w.Id == request.WebhookId && w.OrganizationId == request.OrganizationId,
                                                                      projection: w => new { w.Id, w.Name },
                                                                      cancellationToken: cancellationToken);

        if (webhook == null)
            return Response<int>.Error(0, MessageKey.OrgWebhookNotFound);

        var update = new SetPropertyBuilder<OrganizationWebhook>();

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            var url = request.Url.Trim();
            var problem = await _urlGuard.CheckAsync(url, cancellationToken);

            if (problem != null)
                return Response<int>.Error(0, problem);

            update.SetPropertyValue(w => w.Url, url);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            update.SetPropertyValue(w => w.Name, request.Name.Trim());

        if (request.Events != null)
            update.SetPropertyValue(w => w.Events, request.Events.Distinct().ToList());

        if (request.IsActive.HasValue)
            update.SetPropertyValue(w => w.IsActive, request.IsActive.Value);

        await _webhookRepository.ExecuteUpdateAsync(webhook.Id, update, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrgWebhookUpdated, webhook.Id.ToString(), $"Outgoing webhook '{webhook.Name}' updated.", details: new { request.Url, request.Events, request.IsActive }, cancellationToken: cancellationToken);

        return Response<int>.Success(webhook.Id);
    }
}
