using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of a pull source (a build service that says "ready" and lets storeasy download). Values are explicit and never reused.
/// </summary>
public enum ArtifactPullSourceKind : byte
{
    /// <summary>
    /// EAS build webhook (expo-signature, HMAC-SHA1).
    /// </summary>
    [Description("EAS")]
    Eas = 1,

    /// <summary>
    /// Generic signed webhook (X-Storeasy-Signature, HMAC-SHA256).
    /// </summary>
    [Description("Webhook")]
    Webhook = 2,
}
