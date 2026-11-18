using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.AccountDtos;

namespace Storeasy.Application.Features.Account.Login;

/// <summary>
/// Data transfer object for login operation.
/// </summary>
public record LoginCommand : ICommand<LoginResponseDto>
{
    /// <summary>
    /// Username to log in.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Password to log in.
    /// </summary>
    [LogMasked]
    public string Password { get; set; }

    /// <summary>
    /// Unique Id of the device being logged in. Can be MacAddress or something.
    /// </summary>
    public string DeviceId { get; set; }
}