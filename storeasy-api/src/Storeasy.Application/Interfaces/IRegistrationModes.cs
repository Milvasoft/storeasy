namespace Storeasy.Application.Interfaces;

/// <summary>
/// The registration mode in force: the one a platform manager saved (<see cref="SystemSettingKeys.RegistrationMode"/>), or
/// <c>Storeasy:Registration:Mode</c> from the configuration when none was saved.
/// </summary>
public interface IRegistrationModes
{
    /// <summary>
    /// The mode in force and whether it comes from a saved setting.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(RegistrationMode Mode, bool Saved)> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="mode"/> (null removes the saved setting, so the configuration applies again).
    /// </summary>
    /// <param name="mode"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SetAsync(RegistrationMode? mode, CancellationToken cancellationToken);
}
