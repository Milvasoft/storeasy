using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.AiSettings.SetAiModels;

/// <summary>
/// Handles <see cref="SetAiModelsCommand"/>: stores the choices on the organization's AI settings row and audits the change
/// (the model ids only).
/// </summary>
/// <param name="SettingRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
[Log]
public record SetAiModelsCommandHandler(IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository,
                                        IAuditTrail AuditTrail,
                                        AiOptions Options) : IInterceptable, ICommandHandler<SetAiModelsCommand, AiSettingsDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<AiSettingsDto>> Handle(SetAiModelsCommand request, CancellationToken cancellationToken)
    {
        var setting = await _settingRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId,
                                                                      projection: s => new OrganizationAiSetting
                                                                      {
                                                                          Id = s.Id,
                                                                          ApiKeyLastFour = s.ApiKeyLastFour,
                                                                          ApiKeySetDate = s.ApiKeySetDate,
                                                                          ApiKeySetByUserName = s.ApiKeySetByUserName,
                                                                          ReplyDraftModel = s.ReplyDraftModel,
                                                                          PreReviewModel = s.PreReviewModel,
                                                                      },
                                                                      cancellationToken: cancellationToken);

        if (setting == null)
            return Response<AiSettingsDto>.Error(null, MessageKey.AiKeyNotSet);

        var replyDraftModel = request.ReplyDraftModel ?? setting.ReplyDraftModel;
        var preReviewModel = request.PreReviewModel ?? setting.PreReviewModel;

        await _settingRepository.ExecuteUpdateAsync(setting.Id,
                                                    new SetPropertyBuilder<OrganizationAiSetting>().SetPropertyValue(s => s.ReplyDraftModel, replyDraftModel)
                                                                                                   .SetPropertyValue(s => s.PreReviewModel, preReviewModel),
                                                    cancellationToken);

        var settings = new AiSettingsDto
        {
            HasApiKey = true,
            ApiKeyLastFour = setting.ApiKeyLastFour,
            ApiKeySetDate = setting.ApiKeySetDate,
            ApiKeySetByUserName = setting.ApiKeySetByUserName,
        }.WithModels(_options, replyDraftModel, preReviewModel);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AiModelsChanged,
                                     request.OrganizationId.ToString(),
                                     $"AI models: reply drafts {settings.ReplyDraftModel}, pre-reviews {settings.PreReviewModel}.",
                                     details: new { settings.ReplyDraftModel, settings.PreReviewModel },
                                     cancellationToken: cancellationToken);

        return Response<AiSettingsDto>.Success(settings);
    }
}
