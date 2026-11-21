namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Summed store page metrics. A metric no store in the selection reports is null (Google Play and Microsoft Store report
/// no impressions; only the App Store reports redownloads).
/// </summary>
public class StoreMetricValuesDto
{
    /// <summary>
    /// Times the app was shown in a list.
    /// </summary>
    public long? Impressions { get; set; }

    /// <summary>
    /// Store page views.
    /// </summary>
    public long? PageViews { get; set; }

    /// <summary>
    /// First time downloads.
    /// </summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// Redownloads.
    /// </summary>
    public long? Redownloads { get; set; }

    /// <summary>
    /// Downloads divided by page views (0.05 is 5 percent), over the stores that report both.
    /// </summary>
    public double? ConversionRate { get; set; }

    /// <summary>
    /// Maps summed values.
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static StoreMetricValuesDto From(StoreMetricValues values) => new()
    {
        Impressions = values.Impressions,
        PageViews = values.PageViews,
        Downloads = values.Downloads,
        Redownloads = values.Redownloads,
        ConversionRate = values.ConversionRate,
    };
}
