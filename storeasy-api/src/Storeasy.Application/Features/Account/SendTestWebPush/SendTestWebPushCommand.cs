using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.SendTestWebPush;

/// <summary>
/// Sends a test push to every browser the caller subscribed (rate limited). Returns how many subscriptions it went to.
/// </summary>
public record SendTestWebPushCommand : ICommand<int>
{
}
