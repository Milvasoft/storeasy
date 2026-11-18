using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.IntakeSettings.GetIntakeSettings;

/// <summary>
/// Handles <see cref="GetIntakeSettingsQuery"/>.
/// </summary>
/// <param name="SettingRepository"></param>
public record GetIntakeSettingsQueryHandler(IStoreasyRepositoryBase<IntakeSetting> SettingRepository) : IInterceptable, IQueryHandler<GetIntakeSettingsQuery, IntakeSettingsDto>
{
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = SettingRepository;

    /// <inheritdoc/>
    public async Task<Response<IntakeSettingsDto>> Handle(GetIntakeSettingsQuery request, CancellationToken cancellationToken)
    {
        var setting = await _settingRepository.GetIntakeSettingAsync(request.OrganizationId, cancellationToken);

        return Response<IntakeSettingsDto>.Success(new IntakeSettingsDto { ArtifactRetentionDays = setting.ArtifactRetentionDays, MaxArtifactBytes = setting.MaxArtifactBytes });
    }
}
