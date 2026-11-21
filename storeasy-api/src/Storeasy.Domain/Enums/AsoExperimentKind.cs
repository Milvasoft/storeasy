using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a store listing experiment record is.
/// </summary>
public enum AsoExperimentKind : byte
{
    /// <summary>
    /// App Store product page optimization: an A/B test of up to three treatments against the product page (App Store
    /// Connect API v2 experiments).
    /// </summary>
    [Description("Product page optimization")]
    ProductPageOptimization = 1,

    /// <summary>
    /// App Store custom product page: an extra product page with its own screenshots, previews and promotional text,
    /// reached through its own address (<c>ppid</c>).
    /// </summary>
    [Description("Custom product page")]
    CustomProductPage = 2,

    /// <summary>
    /// Google Play store listing experiment. The Android Publisher API has none, so storeasy prepares the variants and the
    /// member runs it in Play Console (assisted step) and records start, end and result.
    /// </summary>
    [Description("Play store listing experiment")]
    PlayListingExperiment = 3,

    /// <summary>
    /// Google Play custom store listing, prepared in storeasy and set up in Play Console (no API).
    /// </summary>
    [Description("Play custom store listing")]
    PlayCustomListing = 4,
}
