using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.MarkTourPromptSeen;

/// <summary>
/// Records that the caller answered the web app's guided tour invitation (started the tour or closed it), so the
/// invitation does not show again on any device. The account is always the caller's (from the token). Returns the user id.
/// </summary>
public record MarkTourPromptSeenCommand : ICommand<int>
{
}
