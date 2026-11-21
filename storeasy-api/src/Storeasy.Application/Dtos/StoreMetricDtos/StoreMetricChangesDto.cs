using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Change of each metric against the previous period of the same length, as a fraction (0.25 is 25 percent more, -0.1 is
/// 10 percent less). Null when the previous period has no value to compare with. The conversion rate change is the
/// relative change of the rate, not a difference in points.
/// </summary>
public class StoreMetricChangesDto
{
    /// <summary>
    /// Impressions change.
    /// </summary>
    public double? Impressions { get; set; }

    /// <summary>
    /// Page views change.
    /// </summary>
    public double? PageViews { get; set; }

    /// <summary>
    /// Downloads change.
    /// </summary>
    public double? Downloads { get; set; }

    /// <summary>
    /// Redownloads change.
    /// </summary>
    public double? Redownloads { get; set; }

    /// <summary>
    /// Conversion rate change.
    /// </summary>
    public double? ConversionRate { get; set; }

    /// <summary>
    /// Compares two periods.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public static StoreMetricChangesDto From(StoreMetricValues current, StoreMetricValues previous) => new()
    {
        Impressions = StoreMetricRules.Change(current.Impressions, previous.Impressions),
        PageViews = StoreMetricRules.Change(current.PageViews, previous.PageViews),
        Downloads = StoreMetricRules.Change(current.Downloads, previous.Downloads),
        Redownloads = StoreMetricRules.Change(current.Redownloads, previous.Redownloads),
        ConversionRate = StoreMetricRules.Change(current.ConversionRate, previous.ConversionRate),
    };
}
