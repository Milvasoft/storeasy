using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Content;

/// <summary>
/// The content model of a store app (docs/urun-plani.md 2.2, "diff and dry run"): every value storeasy reads from or writes
/// to a store is a string addressed by group, locale and field. Complex values (age rating answers) are kept as their raw
/// JSON text. A change plan is the difference between the desired and the current values of the touched keys.
/// </summary>
public static class ContentKey
{
    private const char _separator = '|';

    /// <summary>
    /// Key of one value. <paramref name="locale"/> is empty for values that are not localized.
    /// </summary>
    /// <param name="group"></param>
    /// <param name="locale"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static string Of(string group, string locale, string field) => $"{group}{_separator}{locale ?? string.Empty}{_separator}{field}";

    /// <summary>
    /// Splits a key made by <see cref="Of"/>.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static (string Group, string Locale, string Field) Parse(string key)
    {
        var parts = key.Split(_separator, 3);

        return parts.Length == 3 ? (parts[0], parts[1].Length == 0 ? null : parts[1], parts[2]) : (key, null, null);
    }
}

/// <summary>
/// Groups of the content model.
/// </summary>
public static class ContentGroups
{
    /// <summary>
    /// App Store app info (name, subtitle, privacy URLs, categories): independent of versions.
    /// </summary>
    public const string AppInfo = "AppInfo";

    /// <summary>
    /// App Store version (localized texts, copyright, release type).
    /// </summary>
    public const string Version = "Version";

    /// <summary>
    /// App level values (content rights declaration, EULA).
    /// </summary>
    public const string App = "App";

    /// <summary>
    /// Play store listing per language.
    /// </summary>
    public const string Listing = "Listing";

    /// <summary>
    /// Play app details.
    /// </summary>
    public const string Details = "Details";

    /// <summary>
    /// Age rating declaration answers (raw JSON values).
    /// </summary>
    public const string AgeRating = "AgeRating";

    /// <summary>
    /// Base territory and price point.
    /// </summary>
    public const string Pricing = "Pricing";

    /// <summary>
    /// Territory availability: field is the territory code, value "true" or "false".
    /// </summary>
    public const string Availability = "Availability";

    /// <summary>
    /// App Review details.
    /// </summary>
    public const string Review = "Review";

    /// <summary>
    /// Build of a version and its export compliance answer.
    /// </summary>
    public const string Build = "Build";

    /// <summary>
    /// Phased release of a version.
    /// </summary>
    public const string Phased = "Phased";

    /// <summary>
    /// Media set: locale and class key address one set, the value is the ordered list of item checksums.
    /// </summary>
    public const string Media = "Media";

    /// <summary>
    /// Play track: field is the track, the value its releases as JSON.
    /// </summary>
    public const string Track = "Track";

    /// <summary>
    /// Play testers: field is the track, the value the Google Group addresses.
    /// </summary>
    public const string Testers = "Testers";
}

/// <summary>
/// A listing field a store has.
/// </summary>
/// <param name="Store">Store.</param>
/// <param name="Group">Content group.</param>
/// <param name="Field">Field name, as the store API names it.</param>
/// <param name="LimitRuleId">Catalog rule that limits the field, if any.</param>
/// <param name="Required">Whether a submission needs it.</param>
/// <param name="IsUrl">Whether the value is an address.</param>
/// <param name="Localized">Whether the field exists per locale.</param>
public sealed record ListingField(StoreKind Store, string Group, string Field, string LimitRuleId, bool Required, bool IsUrl, bool Localized);

/// <summary>
/// Listing fields of each store that storeasy reads and writes.
/// </summary>
public static class ListingFields
{
    /// <summary>
    /// Every listing field.
    /// </summary>
    public static IReadOnlyList<ListingField> All { get; } =
    [
        new(StoreKind.Apple, ContentGroups.AppInfo, "name", "apple.listing-limits.name", true, false, true),
        new(StoreKind.Apple, ContentGroups.AppInfo, "subtitle", "apple.listing-limits.subtitle", false, false, true),
        new(StoreKind.Apple, ContentGroups.AppInfo, "privacyPolicyUrl", null, true, true, true),
        new(StoreKind.Apple, ContentGroups.AppInfo, "privacyChoicesUrl", null, false, true, true),
        new(StoreKind.Apple, ContentGroups.Version, "description", "apple.listing-limits.description", true, false, true),
        new(StoreKind.Apple, ContentGroups.Version, "keywords", "apple.listing-limits.keywords", true, false, true),
        new(StoreKind.Apple, ContentGroups.Version, "promotionalText", "apple.listing-limits.promotional-text", false, false, true),
        new(StoreKind.Apple, ContentGroups.Version, "whatsNew", "apple.listing-limits.whats-new", false, false, true),
        new(StoreKind.Apple, ContentGroups.Version, "supportUrl", null, true, true, true),
        new(StoreKind.Apple, ContentGroups.Version, "marketingUrl", null, false, true, true),
        new(StoreKind.Apple, ContentGroups.App, "eula", null, false, false, false),
        new(StoreKind.Google, ContentGroups.Listing, "title", "google.listing-limits.title", true, false, true),
        new(StoreKind.Google, ContentGroups.Listing, "shortDescription", "google.listing-limits.short-description", true, false, true),
        new(StoreKind.Google, ContentGroups.Listing, "fullDescription", "google.listing-limits.full-description", true, false, true),
        new(StoreKind.Google, ContentGroups.Listing, "video", null, false, true, true),
        new(StoreKind.Google, ContentGroups.Details, "contactEmail", null, true, false, false),
        new(StoreKind.Google, ContentGroups.Details, "contactPhone", null, false, false, false),
        new(StoreKind.Google, ContentGroups.Details, "contactWebsite", null, false, true, false),
        new(StoreKind.Google, ContentGroups.Details, "defaultLanguage", null, true, false, false),
    ];

    /// <summary>
    /// The field named <paramref name="field"/> of <paramref name="store"/> (case insensitive), or null.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static ListingField Find(StoreKind store, string field)
        => All.FirstOrDefault(f => f.Store == store && string.Equals(f.Field, field, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Fields of one store.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static IEnumerable<ListingField> Of(StoreKind store) => All.Where(f => f.Store == store);
}

/// <summary>
/// A store's media class and the catalog rule that describes it.
/// </summary>
/// <param name="Store">Store.</param>
/// <param name="Kind">Screenshot (graphics too) or preview.</param>
/// <param name="ClassKey">The store's own key (App Store display type, Play image type).</param>
/// <param name="RuleId">Catalog rule of the class.</param>
/// <param name="Single">Whether the class holds one item that a new one replaces (Play icon, feature graphic).</param>
public sealed record MediaClass(StoreKind Store, MediaKind Kind, string ClassKey, string RuleId, bool Single = false);

/// <summary>
/// Media classes of each store mapped to catalog rules. The catalog is the source of sizes and requirements; this table only
/// names the store's key of each class.
/// </summary>
public static class MediaClasses
{
    /// <summary>
    /// Every known class.
    /// </summary>
    public static IReadOnlyList<MediaClass> All { get; } =
    [
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_67", "apple.screenshots.iphone-dynamic-island-large"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_65", "apple.screenshots.iphone-face-id-large"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_61", "apple.screenshots.iphone-dynamic-island-medium"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_58", "apple.screenshots.iphone-face-id-medium"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_55", "apple.screenshots.iphone-home-button-large"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_47", "apple.screenshots.iphone-home-button-medium"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_40", "apple.screenshots.iphone-home-button-4in"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPHONE_35", "apple.screenshots.iphone-home-button-3.5in"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_PRO_3GEN_129", "apple.screenshots.ipad-13"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_PRO_129", "apple.screenshots.ipad-12.9"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_PRO_3GEN_11", "apple.screenshots.ipad-11"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_105", "apple.screenshots.ipad-10.5"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_97", "apple.screenshots.ipad-9.7"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_DESKTOP", "apple.screenshots.mac"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_APPLE_TV", "apple.screenshots.apple-tv"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_APPLE_VISION_PRO", "apple.screenshots.apple-vision-pro"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_WATCH_ULTRA", "apple.screenshots.apple-watch"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_WATCH_SERIES_10", "apple.screenshots.apple-watch"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_WATCH_SERIES_7", "apple.screenshots.apple-watch"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_WATCH_SERIES_4", "apple.screenshots.apple-watch"),
        new(StoreKind.Apple, MediaKind.Screenshot, "APP_WATCH_SERIES_3", "apple.screenshots.apple-watch"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_67", "apple.app-previews.iphone-dynamic-island-large"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_65", "apple.app-previews.iphone-face-id-large"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_61", "apple.app-previews.iphone-dynamic-island-medium"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_58", "apple.app-previews.iphone-face-id-medium"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_55", "apple.app-previews.iphone-home-button-large"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_47", "apple.app-previews.iphone-home-button-medium"),
        new(StoreKind.Apple, MediaKind.Preview, "IPHONE_40", "apple.app-previews.iphone-home-button-4in"),
        new(StoreKind.Apple, MediaKind.Preview, "IPAD_PRO_3GEN_129", "apple.app-previews.ipad-13"),
        new(StoreKind.Apple, MediaKind.Preview, "IPAD_PRO_129", "apple.app-previews.ipad-12.9"),
        new(StoreKind.Apple, MediaKind.Preview, "IPAD_PRO_3GEN_11", "apple.app-previews.ipad-11"),
        new(StoreKind.Apple, MediaKind.Preview, "IPAD_105", "apple.app-previews.ipad-10.5"),
        new(StoreKind.Apple, MediaKind.Preview, "IPAD_97", "apple.app-previews.ipad-9.7"),
        new(StoreKind.Apple, MediaKind.Preview, "DESKTOP", "apple.app-previews.mac"),
        new(StoreKind.Apple, MediaKind.Preview, "APPLE_TV", "apple.app-previews.apple-tv"),
        new(StoreKind.Apple, MediaKind.Preview, "APPLE_VISION_PRO", "apple.app-previews.apple-vision-pro"),
        new(StoreKind.Google, MediaKind.Screenshot, "icon", "google.graphics.app-icon", true),
        new(StoreKind.Google, MediaKind.Screenshot, "featureGraphic", "google.graphics.feature-graphic", true),
        new(StoreKind.Google, MediaKind.Screenshot, "phoneScreenshots", "google.screenshots.phone"),
        new(StoreKind.Google, MediaKind.Screenshot, "sevenInchScreenshots", "google.screenshots.tablet-7"),
        new(StoreKind.Google, MediaKind.Screenshot, "tenInchScreenshots", "google.screenshots.tablet-10"),
        new(StoreKind.Google, MediaKind.Screenshot, "wearScreenshots", "google.screenshots.wear-os"),
        new(StoreKind.Google, MediaKind.Screenshot, "tvScreenshots", "google.screenshots.android-tv"),
        new(StoreKind.Google, MediaKind.Screenshot, "tvBanner", "google.graphics.tv-banner", true),
    ];

    /// <summary>
    /// The class with <paramref name="classKey"/> of a store, or null.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="kind"></param>
    /// <param name="classKey"></param>
    /// <returns></returns>
    public static MediaClass Find(StoreKind store, MediaKind kind, string classKey)
        => All.FirstOrDefault(c => c.Store == store && c.Kind == kind && string.Equals(c.ClassKey, classKey, StringComparison.Ordinal));

    /// <summary>
    /// Classes of one store and kind.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static IEnumerable<MediaClass> Of(StoreKind store, MediaKind kind) => All.Where(c => c.Store == store && c.Kind == kind);
}
