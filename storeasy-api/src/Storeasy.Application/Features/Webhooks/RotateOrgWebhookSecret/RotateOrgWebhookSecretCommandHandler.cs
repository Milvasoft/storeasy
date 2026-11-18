using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Notifications;
using System.Text;

namespace Storeasy.Application.Features.Webhooks.RotateOrgWebhookSecret;

/// <summary>
/// Handles <see cref="RotateOrgWebhookSecretCommand"/>. No [Log]: the response carries the secret.
/// </summary>
/// <param name="WebhookRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record RotateOrgWebhookSecretCommandHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository,
                                                   ISecretProtector SecretProtector,
                                                   IAuditTrail AuditTrail,
                                                   TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RotateOrgWebhookSecretCommand, WebhookSecretDto>
{
    private const string _secretPrefix = "whsec_";

    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<WebhookSecretDto>> Handle(RotateOrgWebhookSecretCommand request, CancellationToken cancellationToken)
    {
        var webhook = await _webhookRepository.GetFirstOrDefaultAsync(w => w.Id == request.WebhookId && w.OrganizationId == request.OrganizationId,
                                                                      projection: w => new { w.Id, w.Name, w.Url },
                                                                      cancellationToken: cancellationToken);

        if (webhook == null)
            return Response<WebhookSecretDto>.Error(null, MessageKey.OrgWebhookNotFound);

        var secret = SecureTokens.Create(_secretPrefix);
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), SecretScopes.OrganizationWebhook(request.OrganizationId));

        await _webhookRepository.ExecuteUpdateAsync(webhook.Id,
                                                    new SetPropertyBuilder<OrganizationWebhook>().SetPropertyValue(w => w.EncryptedSecret, sealedSecret.EncryptedSecret)
                                                                                                 .SetPropertyValue(w => w.EncryptedDataKey, sealedSecret.EncryptedDataKey)
                                                                                                 .SetPropertyValue(w => w.KeyVersion, sealedSecret.KeyVersion),
                                                    cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrgWebhookUpdated, webhook.Id.ToString(), $"Secret of outgoing webhook '{webhook.Name}' rotated.", cancellationToken: cancellationToken);

        return Response<WebhookSecretDto>.Success(new WebhookSecretDto { Id = webhook.Id, Url = webhook.Url, Secret = secret, SecretSetDate = _timeProvider.GetUtcNow().UtcDateTime });
    }
}
