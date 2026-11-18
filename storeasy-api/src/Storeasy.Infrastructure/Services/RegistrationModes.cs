using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IRegistrationModes"/>: one <see cref="SystemSetting"/> row read per call (registration and the
/// public settings are rare and rate limited), the configuration when there is none or it does not parse. A single team
/// instance (<see cref="DeploymentMode.SingleTeam"/>) is always <see cref="RegistrationMode.InviteOnly"/>: people join the
/// team by invitation, whatever is configured or saved.
/// </summary>
/// <param name="settingRepository"></param>
/// <param name="registrationOptions"></param>
/// <param name="deploymentOptions"></param>
public sealed class RegistrationModes(IStoreasyRepositoryBase<SystemSetting> settingRepository, RegistrationOptions registrationOptions, DeploymentOptions deploymentOptions) : IRegistrationModes
{
    private readonly IStoreasyRepositoryBase<SystemSetting> _settingRepository = settingRepository;
    private readonly RegistrationOptions _registrationOptions = registrationOptions;
    private readonly DeploymentOptions _deploymentOptions = deploymentOptions;

    /// <inheritdoc/>
    public async Task<(RegistrationMode Mode, bool Saved)> GetAsync(CancellationToken cancellationToken)
    {
        if (_deploymentOptions.IsSingleTeam)
            return (RegistrationMode.InviteOnly, false);

        var saved = await _settingRepository.GetFirstOrDefaultAsync(s => s.Key == SystemSettingKeys.RegistrationMode, projection: s => s.Value, cancellationToken: cancellationToken);

        return Enum.TryParse<RegistrationMode>(saved, out var mode) && Enum.IsDefined(mode) ? (mode, true) : (_registrationOptions.Mode, false);
    }

    /// <inheritdoc/>
    public async Task SetAsync(RegistrationMode? mode, CancellationToken cancellationToken)
    {
        if (mode == null)
        {
            await _settingRepository.ExecuteDeleteAsync(s => s.Key == SystemSettingKeys.RegistrationMode, cancellationToken: cancellationToken);

            return;
        }

        var updated = await _settingRepository.ExecuteUpdateAsync(s => s.Key == SystemSettingKeys.RegistrationMode,
                                                                  new SetPropertyBuilder<SystemSetting>().SetPropertyValue(s => s.Value, mode.Value.ToString()),
                                                                  cancellationToken);

        if (updated == 0)
            await _settingRepository.AddAsync(new SystemSetting { Key = SystemSettingKeys.RegistrationMode, Value = mode.Value.ToString() }, cancellationToken);
    }
}
