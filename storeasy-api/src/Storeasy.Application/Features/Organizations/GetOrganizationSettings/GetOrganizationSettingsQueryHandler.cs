using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.GetOrganizationSettings;

/// <summary>
/// Handles <see cref="GetOrganizationSettingsQuery"/>.
/// </summary>
/// <param name="organizationRepository"></param>
/// <param name="settingRepository"></param>
public class GetOrganizationSettingsQueryHandler(IStoreasyRepositoryBase<Organization> organizationRepository, IStoreasyRepositoryBase<IntakeSetting> settingRepository) : IInterceptable, IQueryHandler<GetOrganizationSettingsQuery, OrganizationSettingsDto>
{
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = organizationRepository;
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = settingRepository;

    /// <inheritdoc/>
    public async Task<Response<OrganizationSettingsDto>> Handle(GetOrganizationSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Id == request.OrganizationId,
                                                                            projection: o => new OrganizationSettingsDto { AuditRetentionDays = o.AuditRetentionDays, DefaultLocale = o.DefaultLocale },
                                                                            cancellationToken: cancellationToken);

        if (settings == null)
            return Response<OrganizationSettingsDto>.Error(null, MessageKey.OrganizationNotFound);

        settings.ArtifactRetentionDays = (await _settingRepository.GetIntakeSettingAsync(request.OrganizationId, cancellationToken)).ArtifactRetentionDays;
        settings.SeenNotificationRetentionDays = NotificationKinds.SeenRetentionDays;
        settings.UnseenNotificationRetentionDays = NotificationKinds.UnseenRetentionDays;

        return Response<OrganizationSettingsDto>.Success(settings);
    }
}
