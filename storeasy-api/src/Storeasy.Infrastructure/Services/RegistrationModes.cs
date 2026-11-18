using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IRegistrationModes"/>: one <see cref="SystemSetting"/> row read per call (registration and the
/// public settings are rare and rate limited), the configuration when there is none or it does not parse.
/// </summary>
/// <param name="settingRepository"></param>
/// <param name="registrationOptions"></param>
public sealed class RegistrationModes(IStoreasyRepositoryBase<SystemSetting> settingRepository, RegistrationOptions registrationOptions) : IRegistrationModes
{
    private readonly IStoreasyRepositoryBase<SystemSetting> _settingRepository = settingRepository;
    private readonly RegistrationOptions _registrationOptions = registrationOptions;

    /// <inheritdoc/>
    public async Task<(RegistrationMode Mode, bool Saved)> GetAsync(CancellationToken cancellationToken)
    {
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
