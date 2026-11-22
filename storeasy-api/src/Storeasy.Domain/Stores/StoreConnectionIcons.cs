using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Stores;

/// <summary>
/// The icons a member can pick for a store connection, so its records are told apart at a glance (the web draws them;
/// the API stores only the key). A fixed list, no uploads. The store logos only identify their own store, so each
/// is allowed only on a connection of that store; the generic icons fit any store.
/// </summary>
public static class StoreConnectionIcons
{
    /// <summary>
    /// Column length of <see cref="StoreConnection.Icon"/>; longer than every key.
    /// </summary>
    public const int MaxLength = 20;

    /// <summary>App Store logo (Apple connections only).</summary>
    public const string AppStore = "appstore";

    /// <summary>Google Play logo (Google connections only).</summary>
    public const string GooglePlay = "googleplay";

    /// <summary>Microsoft Store logo (Microsoft connections only).</summary>
    public const string MsStore = "msstore";

    /// <summary>winget logo (winget connections only).</summary>
    public const string Winget = "winget";

    /// <summary>
    /// Chrome Web Store.
    /// </summary>
    public const string ChromeWebStore = "chromewebstore";

    /// <summary>
    /// Firefox Add-ons.
    /// </summary>
    public const string FirefoxAddons = "firefoxaddons";

    /// <summary>
    /// The generic icons, in the order the web shows them.
    /// </summary>
    public static IReadOnlyList<string> Generic { get; } = ["phone", "tablet", "bag", "window", "globe", "box", "game", "rocket"];

    /// <summary>
    /// The store logo of <paramref name="store"/> (<see cref="StoreChannels"/>).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static string LogoOf(StoreKind store) => StoreChannels.Of(store)?.LogoIcon;

    /// <summary>
    /// Whether <paramref name="icon"/> may be stored on a connection of <paramref name="store"/>. Null or empty (no
    /// icon, the web shows the store's text code) is allowed; a key is compared exactly (lower case).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="icon"></param>
    /// <returns></returns>
    public static bool IsAllowed(StoreKind store, string icon)
    {
        if (string.IsNullOrEmpty(icon))
            return true;

        if (icon == LogoOf(store))
            return true;

        return Generic.Contains(icon);
    }

    /// <summary>
    /// Whether <paramref name="icon"/> is one of the keys (any store), or null or empty.
    /// </summary>
    /// <param name="icon"></param>
    /// <returns></returns>
    public static bool IsKnown(string icon) => string.IsNullOrEmpty(icon) || StoreChannels.All.Any(c => c.LogoIcon == icon) || Generic.Contains(icon);

    /// <summary>
    /// The value to store: null for an empty choice, the key otherwise.
    /// </summary>
    /// <param name="icon"></param>
    /// <returns></returns>
    public static string Normalize(string icon) => string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
}
