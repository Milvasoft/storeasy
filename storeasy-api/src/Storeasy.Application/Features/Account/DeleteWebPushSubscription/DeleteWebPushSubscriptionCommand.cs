using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.DeleteWebPushSubscription;

/// <summary>
/// Removes one of the caller's browser subscriptions, by its id (settings list) or its address (the browser
/// unsubscribing). Returns the removed id.
/// </summary>
public record DeleteWebPushSubscriptionCommand : ICommand<int>
{
    /// <summary>
    /// Subscription id.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Push service address.
    /// </summary>
    [LogMasked]
    public string Endpoint { get; set; }
}
