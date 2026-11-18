using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.IntakeSettings.UpdateIntakeSettings;

/// <summary>
/// Handles <see cref="UpdateIntakeSettingsCommand"/>.
/// </summary>
/// <param name="SettingRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateIntakeSettingsCommandHandler(IStoreasyRepositoryBase<IntakeSetting> SettingRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateIntakeSettingsCommand, int>
{
    private readonly IStoreasyRepositoryBase<IntakeSetting> _settingRepository = SettingRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateIntakeSettingsCommand request, CancellationToken cancellationToken)
    {
        var current = await _settingRepository.GetIntakeSettingAsync(request.OrganizationId, cancellationToken);

        if (current.Id == 0)
        {
            await _settingRepository.AddAsync(new IntakeSetting { OrganizationId = request.OrganizationId, ArtifactRetentionDays = request.ArtifactRetentionDays, MaxArtifactBytes = request.MaxArtifactBytes }, cancellationToken);
        }
        else
        {
            await _settingRepository.ExecuteUpdateAsync(current.Id,
                                                        new SetPropertyBuilder<IntakeSetting>().SetPropertyValue(s => s.ArtifactRetentionDays, request.ArtifactRetentionDays)
                                                                                               .SetPropertyValue(s => s.MaxArtifactBytes, request.MaxArtifactBytes),
                                                        cancellationToken);
        }

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.IntakeSettingsUpdated,
                                     request.OrganizationId.ToString(),
                                     $"Packages are kept {request.ArtifactRetentionDays} days.",
                                     details: new { request.ArtifactRetentionDays, request.MaxArtifactBytes },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(request.ArtifactRetentionDays);
    }
}
