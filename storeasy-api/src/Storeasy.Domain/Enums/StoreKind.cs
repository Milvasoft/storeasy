using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A store storeasy talks to. Shared models never assume a fixed set: an app may live in one, two or all three stores.
/// </summary>
public enum StoreKind : byte
{
    /// <summary>
    /// App Store Connect (API key: .p8 private key, key id, issuer id).
    /// </summary>
    [Description("App Store Connect")]
    Apple = 1,

    /// <summary>
    /// Google Play Developer API (service account JSON key).
    /// </summary>
    [Description("Google Play")]
    Google = 2,

    /// <summary>
    /// Microsoft Store (Partner Center submission APIs, Microsoft Entra app). Connections and sync arrive in phase 5;
    /// until then the store engine answers "not supported yet".
    /// </summary>
    [Description("Microsoft Store")]
    Microsoft = 3,
}
