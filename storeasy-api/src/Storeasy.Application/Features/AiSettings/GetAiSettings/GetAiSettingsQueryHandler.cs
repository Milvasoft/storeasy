using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.AiSettings.GetAiSettings;

/// <summary>
/// Handles <see cref="GetAiSettingsQuery"/>. The sealed key columns are never read.
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
        var settings = await _settingRepository.GetFirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId,
                                                                       projection: s => new AiSettingsDto
                                                                       {
                                                                           HasApiKey = true,
                                                                           ApiKeyLastFour = s.ApiKeyLastFour,
                                                                           ApiKeySetDate = s.ApiKeySetDate,
                                                                           ApiKeySetByUserName = s.ApiKeySetByUserName,
                                                                       },
                                                                       cancellationToken: cancellationToken) ?? new AiSettingsDto();

        settings.Model = _options.Model;

        return Response<AiSettingsDto>.Success(settings);
    }
}
