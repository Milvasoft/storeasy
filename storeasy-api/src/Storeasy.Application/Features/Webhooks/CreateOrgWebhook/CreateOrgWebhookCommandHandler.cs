using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Notifications;
using System.Text;

namespace Storeasy.Application.Features.Webhooks.CreateOrgWebhook;

/// <summary>
/// Handles <see cref="CreateOrgWebhookCommand"/>. No [Log]: the response carries the secret.
/// </summary>
/// <param name="WebhookRepository"></param>
/// <param name="UrlGuard"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record CreateOrgWebhookCommandHandler(IStoreasyRepositoryBase<OrganizationWebhook> WebhookRepository,
                                             IWebhookUrlGuard UrlGuard,
                                             ISecretProtector SecretProtector,
                                             IAuditTrail AuditTrail,
                                             OrgWebhookOptions Options,
                                             TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreateOrgWebhookCommand, WebhookSecretDto>
{
    private const string _secretPrefix = "whsec_";

    private readonly IStoreasyRepositoryBase<OrganizationWebhook> _webhookRepository = WebhookRepository;
    private readonly IWebhookUrlGuard _urlGuard = UrlGuard;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly OrgWebhookOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<WebhookSecretDto>> Handle(CreateOrgWebhookCommand request, CancellationToken cancellationToken)
    {
        var url = request.Url.Trim();
        var problem = await _urlGuard.CheckAsync(url, cancellationToken);

        if (problem != null)
            return Response<WebhookSecretDto>.Error(null, problem);

        var existing = await _webhookRepository.GetAllAsync(w => w.OrganizationId == request.OrganizationId,
                                                            projection: w => new OrganizationWebhook { Id = w.Id },
                                                            cancellationToken: cancellationToken);

        if (existing.Count >= _options.MaxSubscriptions)
            return Response<WebhookSecretDto>.Error(null, MessageKey.OrgWebhookLimitReached);

        var secret = SecureTokens.Create(_secretPrefix);
        var sealedSecret = _secretProtector.Protect(Encoding.UTF8.GetBytes(secret), SecretScopes.OrganizationWebhook(request.OrganizationId));

        var webhook = new OrganizationWebhook
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Url = url,
            Events = [.. (request.Events ?? []).Distinct()],
            IsActive = true,
            EncryptedSecret = sealedSecret.EncryptedSecret,
            EncryptedDataKey = sealedSecret.EncryptedDataKey,
            KeyVersion = sealedSecret.KeyVersion,
        };

        await _webhookRepository.AddAsync(webhook, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrgWebhookAdded, webhook.Id.ToString(), $"Outgoing webhook '{webhook.Name}' added.", details: new { webhook.Url, webhook.Events }, cancellationToken: cancellationToken);

        return Response<WebhookSecretDto>.Success(new WebhookSecretDto { Id = webhook.Id, Url = url, Secret = secret, SecretSetDate = _timeProvider.GetUtcNow().UtcDateTime });
    }
}
