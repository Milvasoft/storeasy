using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Settings.CompleteSetup;

/// <summary>
/// First run setup of a single team instance (anonymous): creates the administrator and the team, then signs the
/// administrator in. Requires the one time setup code from the API log (or <c>Storeasy:Bootstrap:SetupCode</c>). Works
/// only while the instance has no user; afterwards it is refused with <see cref="MessageKey.SetupAlreadyCompleted"/>.
/// In multi tenant mode the endpoint does not exist (<see cref="MessageKey.PageNotFound"/>).
/// </summary>
public record CompleteSetupCommand : ICommand<LoginResponseDto>
{
    /// <summary>
    /// The one time setup code (<c>XXXX-XXXX-XXXX</c>) written to the API log at startup.
    /// </summary>
    [LogMasked]
    public string SetupCode { get; set; }

    /// <summary>
    /// First name of the administrator.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Last name of the administrator (optional).
    /// </summary>
    public string Surname { get; set; }

    /// <summary>
    /// E-mail address of the administrator; it is also the sign in name.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Password of the administrator.
    /// </summary>
    [LogMasked]
    public string Password { get; set; }

    /// <summary>
    /// Name of the team (the single organization of the instance).
    /// </summary>
    public string OrganizationName { get; set; }

    /// <summary>
    /// Unique id of the device that signs in (optional, like the login).
    /// </summary>
    public string DeviceId { get; set; }
}
