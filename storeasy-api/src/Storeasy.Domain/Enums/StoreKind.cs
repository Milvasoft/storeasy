using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A store or distribution channel storeasy talks to. Shared models never assume a fixed set: an app may live in any
/// number of them. What each one supports (credential expiry, reviews, metrics, its console) is in
/// <see cref="Stores.StoreChannels"/>; docs/adding-a-store.md lists every place a new value is wired in.
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
    /// Microsoft Store (Partner Center submission APIs, Microsoft Entra app).
    /// </summary>
    [Description("Microsoft Store")]
    Microsoft = 3,

    /// <summary>
    /// Windows Package Manager community repository (<c>microsoft/winget-pkgs</c> on GitHub). Manifests go in as pull
    /// requests from the owner's fork, opened with the owner's GitHub token; a merged pull request is a published version.
    /// </summary>
    [Description("winget")]
    Winget = 4,

    /// <summary>
    /// Chrome Web Store (browser extensions), through the Chrome Web Store API v2 with a Google service account or an
    /// OAuth client and refresh token, and the publisher id. Items are added by their extension id.
    /// </summary>
    [Description("Chrome Web Store")]
    Chrome = 5,
}
