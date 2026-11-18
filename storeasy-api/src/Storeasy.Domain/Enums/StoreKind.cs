using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A store storeasy talks to.
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
}
