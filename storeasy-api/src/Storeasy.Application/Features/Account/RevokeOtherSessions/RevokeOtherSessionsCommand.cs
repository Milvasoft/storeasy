using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.RevokeOtherSessions;

/// <summary>
/// Signs out every session of the caller except the one of this request. Returns how many were signed out.
/// </summary>
public record RevokeOtherSessionsCommand : ICommand<int>
{
}
