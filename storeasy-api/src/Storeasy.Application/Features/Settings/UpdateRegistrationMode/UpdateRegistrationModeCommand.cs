using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Settings.UpdateRegistrationMode;

/// <summary>
/// Saves who may register, overriding <c>Storeasy:Registration:Mode</c>; null removes the saved mode so the
/// configuration applies again. Returns the mode in force.
/// </summary>
public record UpdateRegistrationModeCommand : ICommand<RegistrationMode>
{
    /// <summary>
    /// New mode, or null for the configured one.
    /// </summary>
    public RegistrationMode? Mode { get; set; }
}
