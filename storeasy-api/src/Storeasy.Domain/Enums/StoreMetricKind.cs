using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A store page performance metric. The first four are stored per day; <see cref="ConversionRate"/> is computed on read
/// (downloads divided by page views). Values are explicit and never reused.
/// </summary>
public enum StoreMetricKind : byte
{
    /// <summary>The app was shown in a list (App Store only; Google Play and Microsoft Store do not report it).</summary>
    [Description("Impressions")]
    Impressions = 1,

    /// <summary>The store page was opened (App Store product page views, Play store listing visitors, Microsoft page views).</summary>
    [Description("Page views")]
    PageViews = 2,

    /// <summary>First time downloads (App Store first-time downloads, Play store listing acquisitions, Microsoft acquisitions).</summary>
    [Description("Downloads")]
    Downloads = 3,

    /// <summary>Downloads by people who had the app before (App Store only).</summary>
    [Description("Redownloads")]
    Redownloads = 4,

    /// <summary>Downloads divided by page views, computed on read over the rows that report both.</summary>
    [Description("Conversion rate")]
    ConversionRate = 5,
}
