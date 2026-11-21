namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Store page performance of an app over a date range: totals with the change against the previous period, the daily
/// series and the breakdowns by source, country and store. Every number is one slice of the stored rows (the chosen
/// country or every country, the chosen source or every source), so totals and breakdowns never count a download twice.
/// </summary>
public class StoreMetricsReportDto
{
    /// <summary>
    /// First day of the range.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// Last day of the range.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// First day of the previous period (same length, right before <see cref="From"/>).
    /// </summary>
    public DateOnly PreviousFrom { get; set; }

    /// <summary>
    /// Last day of the previous period.
    /// </summary>
    public DateOnly PreviousTo { get; set; }

    /// <summary>
    /// Totals of the range.
    /// </summary>
    public StoreMetricValuesDto Totals { get; set; }

    /// <summary>
    /// Totals of the previous period.
    /// </summary>
    public StoreMetricValuesDto PreviousTotals { get; set; }

    /// <summary>
    /// Change against the previous period.
    /// </summary>
    public StoreMetricChangesDto Changes { get; set; }

    /// <summary>
    /// Days with data, oldest first.
    /// </summary>
    public List<StoreMetricDayDto> Days { get; set; } = [];

    /// <summary>
    /// Totals per traffic source, most page views first.
    /// </summary>
    public List<StoreMetricBreakdownDto> Sources { get; set; } = [];

    /// <summary>
    /// Totals per country, most downloads first (at most 50).
    /// </summary>
    public List<StoreMetricBreakdownDto> Countries { get; set; } = [];

    /// <summary>
    /// Totals per store.
    /// </summary>
    public List<StoreMetricBreakdownDto> Stores { get; set; } = [];
}
