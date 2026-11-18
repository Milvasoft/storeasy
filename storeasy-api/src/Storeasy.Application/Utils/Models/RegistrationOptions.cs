namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Self service registration settings (configuration section <see cref="SectionName"/>), bound once and registered as a
/// singleton.
/// </summary>
public class RegistrationOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Registration";

    /// <summary>
    /// Who may register. <see cref="RegistrationMode.Open"/> by default.
    /// </summary>
    public RegistrationMode Mode { get; set; } = RegistrationMode.Open;
}
