using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Who calls an inbound webhook endpoint and how its signature is checked. Values are explicit and never reused.
/// </summary>
public enum WebhookSource : byte
{
    /// <summary>
    /// App Store Connect: <c>x-apple-signature: hmacsha256=&lt;hex&gt;</c> over the raw body.
    /// </summary>
    [Description("App Store Connect")]
    AppStoreConnect = 1,

    /// <summary>
    /// Any other sender with storeasy's own scheme: HMAC-SHA256 over <c>{timestamp}.{body}</c> with a timestamp and an event id.
    /// </summary>
    [Description("Signed webhook")]
    Signed = 2,
}
