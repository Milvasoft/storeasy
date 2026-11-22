using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Stores;

/// <summary>
/// What shared code needs to know about a store or distribution channel, in one table: the key used in URLs and the rules
/// catalog, the display name, the logo icon key, and which shared features apply to it. Shared code asks this table
/// instead of switching over <see cref="StoreKind"/>, so a new channel is one row here plus its own engine
/// (docs/adding-a-store.md). The web app mirrors the table in <c>storeasy-app/src/lib/stores/channels.ts</c>.
/// </summary>
public static class StoreChannels
{
    private static readonly StoreChannelInfo[] _all =
    [
        new(StoreKind.Apple, "apple", "App Store", StoreConnectionIcons.AppStore, CredentialExpiry.None, HasReviews: true, HasMetrics: true, "https://appstoreconnect.apple.com/apps"),
        new(StoreKind.Google, "google", "Google Play", StoreConnectionIcons.GooglePlay, CredentialExpiry.None, HasReviews: true, HasMetrics: true, "https://play.google.com/console"),
        new(StoreKind.Microsoft, "microsoft", "Microsoft Store", StoreConnectionIcons.MsStore, CredentialExpiry.Required, HasReviews: false, HasMetrics: true, "https://partner.microsoft.com/dashboard/apps-and-games/overview"),
        new(StoreKind.Winget, "winget", "winget", StoreConnectionIcons.Winget, CredentialExpiry.Optional, HasReviews: false, HasMetrics: false, "https://github.com/microsoft/winget-pkgs/pulls"),
        new(StoreKind.Firefox, "firefox", "Firefox Add-ons", StoreConnectionIcons.FirefoxAddons, CredentialExpiry.None, HasReviews: true, HasMetrics: false, "https://addons.mozilla.org/developers/addons"),
    ];

    private static readonly Dictionary<StoreKind, StoreChannelInfo> _byStore = _all.ToDictionary(c => c.Store);

    private static readonly Dictionary<string, StoreChannelInfo> _byKey = _all.ToDictionary(c => c.Key, StringComparer.Ordinal);

    /// <summary>
    /// Every channel, in <see cref="StoreKind"/> order.
    /// </summary>
    public static IReadOnlyList<StoreChannelInfo> All => _all;

    /// <summary>
    /// The row of <paramref name="store"/>; null for a value without a row (never for a defined <see cref="StoreKind"/>,
    /// a unit test checks that).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static StoreChannelInfo Of(StoreKind store) => _byStore.GetValueOrDefault(store);

    /// <summary>
    /// The channel with the key <paramref name="key"/> (<c>apple</c>, <c>winget</c>), or null.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static StoreChannelInfo OfKey(string key) => key != null && _byKey.TryGetValue(key, out var channel) ? channel : null;

    /// <summary>
    /// Display name of <paramref name="store"/> (falls back to the enum name).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static string DisplayName(StoreKind store) => Of(store)?.DisplayName ?? store.ToString();

    /// <summary>
    /// Whether a connection of <paramref name="store"/> records the expiry date of its credential (Entra client secrets,
    /// GitHub tokens).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static bool CredentialExpires(StoreKind store) => (Of(store)?.CredentialExpiry ?? CredentialExpiry.None) != CredentialExpiry.None;

    /// <summary>
    /// Whether a connection of <paramref name="store"/> must be given the expiry date of its credential.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static bool CredentialExpiryRequired(StoreKind store) => Of(store)?.CredentialExpiry == CredentialExpiry.Required;

    /// <summary>
    /// Stores with user reviews storeasy reads and answers.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static bool HasReviews(StoreKind store) => Of(store)?.HasReviews == true;

    /// <summary>
    /// Stores with store page metrics (impressions, page views, installs).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static bool HasMetrics(StoreKind store) => Of(store)?.HasMetrics == true;
}

/// <summary>
/// One row of <see cref="StoreChannels"/>.
/// </summary>
/// <param name="Store">The store.</param>
/// <param name="Key">Lower case key: the <c>?store=</c> value of the web app and the rules catalog store key.</param>
/// <param name="DisplayName">Name shown to people and given to AI prompts.</param>
/// <param name="LogoIcon">Connection icon key of the store's own logo (<see cref="StoreConnectionIcons"/>).</param>
/// <param name="CredentialExpiry">Whether a connection records the expiry date of its credential, and whether it must.</param>
/// <param name="HasReviews">Whether the store has user reviews storeasy reads.</param>
/// <param name="HasMetrics">Whether the store has store page metrics storeasy reads.</param>
/// <param name="ConsoleUrl">The store's own console, where the owner acts on what storeasy cannot do.</param>
public sealed record StoreChannelInfo(StoreKind Store, string Key, string DisplayName, string LogoIcon, CredentialExpiry CredentialExpiry, bool HasReviews, bool HasMetrics, string ConsoleUrl);

/// <summary>
/// Whether a store connection records when its credential expires.
/// </summary>
public enum CredentialExpiry : byte
{
    /// <summary>
    /// The credential has no expiry storeasy tracks (App Store Connect keys, Google service account keys).
    /// </summary>
    None = 0,

    /// <summary>
    /// The owner may give the expiry date (a GitHub token without an expiry has none).
    /// </summary>
    Optional = 1,

    /// <summary>
    /// The owner must give the expiry date (Microsoft Entra client secrets always expire).
    /// </summary>
    Required = 2,
}
