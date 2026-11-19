using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;
using System.Text;

namespace Storeasy.Application.Features.AiSettings.SetAiKey;

/// <summary>
/// Handles <see cref="SetAiKeyCommand"/>. No [Log]: the request carries the key, and the audit log already records the
/// change without it.
/// </summary>
/// <param name="SettingRepository"></param>
/// <param name="DraftWriter"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record SetAiKeyCommandHandler(IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                     IReplyDraftWriter DraftWriter,
                                     ISecretProtector SecretProtector,
                                     IAuditTrail AuditTrail,
                                     ICurrentUser CurrentUser,
                                     AiOptions Options,
                                     TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SetAiKeyCommand, AiSettingsDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly IReplyDraftWriter _draftWriter = DraftWriter;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly AiOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AiSettingsDto>> Handle(SetAiKeyCommand request, CancellationToken cancellationToken)
    {
        var apiKey = request.ApiKey.Trim();
        var check = await _draftWriter.CheckKeyAsync(apiKey, cancellationToken);

        if (check == AiKeyCheck.Rejected)
            return Response<AiSettingsDto>.Error(null, MessageKey.AiKeyRejected);

        var sealedKey = _secretProtector.Protect(Encoding.UTF8.GetBytes(apiKey), SecretScopes.AiProviderKey(request.OrganizationId));
        var lastFour = apiKey[^OrganizationAiSetting.LastFourLength..];
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userName = _currentUser.UserName;

        var existing = await _settingRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId,
                                                                       projection: s => new OrganizationAiSetting { Id = s.Id, ReplyDraftModel = s.ReplyDraftModel, PreReviewModel = s.PreReviewModel },
                                                                       cancellationToken: cancellationToken);
        var existingId = existing?.Id;

        if (existingId.HasValue)
        {
            await _settingRepository.ExecuteUpdateAsync(existingId.Value,
                                                        new SetPropertyBuilder<OrganizationAiSetting>().SetPropertyValue(s => s.EncryptedApiKey, sealedKey.EncryptedSecret)
                                                                                                       .SetPropertyValue(s => s.EncryptedDataKey, sealedKey.EncryptedDataKey)
                                                                                                       .SetPropertyValue(s => s.KeyVersion, sealedKey.KeyVersion)
                                                                                                       .SetPropertyValue(s => s.ApiKeyLastFour, lastFour)
                                                                                                       .SetPropertyValue(s => s.ApiKeySetDate, now)
                                                                                                       .SetPropertyValue(s => s.ApiKeySetByUserName, userName),
                                                        cancellationToken);
        }
        else
        {
            await _settingRepository.AddAsync(new OrganizationAiSetting
            {
                OrganizationId = request.OrganizationId,
                EncryptedApiKey = sealedKey.EncryptedSecret,
                EncryptedDataKey = sealedKey.EncryptedDataKey,
                KeyVersion = sealedKey.KeyVersion,
                ApiKeyLastFour = lastFour,
                ApiKeySetDate = now,
                ApiKeySetByUserName = userName,
            }, cancellationToken);
        }

        var verified = check == AiKeyCheck.Valid;

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AiKeySet,
                                     request.OrganizationId.ToString(),
                                     existingId.HasValue ? "Anthropic API key replaced." : "Anthropic API key added.",
                                     details: new { Verified = verified },
                                     cancellationToken: cancellationToken);

        var settings = new AiSettingsDto
        {
            HasApiKey = true,
            ApiKeyLastFour = lastFour,
            ApiKeySetDate = now,
            ApiKeySetByUserName = userName,
            ApiKeyVerified = verified,
        }.WithModels(_options, existing?.ReplyDraftModel, existing?.PreReviewModel);

        return verified
            ? Response<AiSettingsDto>.Success(settings)
            : Response<AiSettingsDto>.Success(settings, MessageKey.AiKeyUnverified, MessageType.Warning);
    }
}
