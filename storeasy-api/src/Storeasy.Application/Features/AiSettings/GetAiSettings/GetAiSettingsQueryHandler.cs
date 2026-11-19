using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.AiSettings.GetAiSettings;

/// <summary>
/// Handles <see cref="GetAiSettingsQuery"/>. The sealed key columns are never read. Without a row the default models are
/// returned.
/// </summary>
/// <param name="SettingRepository"></param>
/// <param name="Options"></param>
public record GetAiSettingsQueryHandler(IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository, AiOptions Options) : IInterceptable, IQueryHandler<GetAiSettingsQuery, AiSettingsDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly AiOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<AiSettingsDto>> Handle(GetAiSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await _settingRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId,
                                                                       projection: s => new OrganizationAiSetting
                                                                       {
                                                                           ApiKeyLastFour = s.ApiKeyLastFour,
                                                                           ApiKeySetDate = s.ApiKeySetDate,
                                                                           ApiKeySetByUserName = s.ApiKeySetByUserName,
                                                                           ReplyDraftModel = s.ReplyDraftModel,
                                                                           PreReviewModel = s.PreReviewModel,
                                                                       },
                                                                       cancellationToken: cancellationToken);

        var settings = setting == null
            ? new AiSettingsDto()
            : new AiSettingsDto
            {
                HasApiKey = true,
                ApiKeyLastFour = setting.ApiKeyLastFour,
                ApiKeySetDate = setting.ApiKeySetDate,
                ApiKeySetByUserName = setting.ApiKeySetByUserName,
            };

        return Response<AiSettingsDto>.Success(settings.WithModels(_options, setting?.ReplyDraftModel, setting?.PreReviewModel));
    }
}
