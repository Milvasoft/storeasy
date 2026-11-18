using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.RevokeSession;

/// <summary>
/// Signs out one of the caller's sessions (a device): its refresh token and access token stop working and its live
/// connection closes; the other sessions keep working. Returns the session id.
/// </summary>
public record RevokeSessionCommand : ICommand<long>
{
    /// <summary>
    /// Session id.
    /// </summary>
    public long Id { get; set; }
}
