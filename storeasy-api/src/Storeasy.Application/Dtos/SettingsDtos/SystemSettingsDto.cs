namespace Storeasy.Application.Dtos.SettingsDtos;

/// <summary>
/// Instance settings a platform manager may change.
/// </summary>
public class SystemSettingsDto
{
    /// <summary>
    /// Registration mode in force.
    /// </summary>
    public RegistrationMode RegistrationMode { get; set; }

    /// <summary>
    /// Whether <see cref="RegistrationMode"/> was saved here (true) or comes from <c>Storeasy:Registration:Mode</c>.
    /// </summary>
    public bool RegistrationModeSaved { get; set; }

    /// <summary>
    /// The configured registration mode (what applies when the saved one is removed).
    /// </summary>
    public RegistrationMode ConfiguredRegistrationMode { get; set; }

    /// <summary>
    /// Deployment mode of the instance (<c>Storeasy:Mode</c>); in single team mode the registration mode is fixed.
    /// </summary>
    public DeploymentMode Mode { get; set; }
}
