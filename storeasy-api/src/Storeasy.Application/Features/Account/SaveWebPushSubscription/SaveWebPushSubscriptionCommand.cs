using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;

namespace Storeasy.Application.Features.Account.SaveWebPushSubscription;

/// <summary>
/// Saves the caller's browser push subscription (the fields of <c>PushSubscription.toJSON()</c>, made with the VAPID
/// public key from <c>system/public-settings</c>). The same address saved again updates the row; a browser that moves to
/// another account moves with it. Returns the subscription id.
/// </summary>
public record SaveWebPushSubscriptionCommand : ICommand<int>
{
    /// <summary>
    /// Push service address (https).
    /// </summary>
    [LogMasked]
    public string Endpoint { get; set; }

    /// <summary>
    /// <c>keys.p256dh</c>, base64url.
    /// </summary>
    public string P256dh { get; set; }

    /// <summary>
    /// <c>keys.auth</c>, base64url.
    /// </summary>
    [LogMasked]
    public string Auth { get; set; }

    /// <summary>
    /// Browser description for the settings list.
    /// </summary>
    public string UserAgent { get; set; }
}
