namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Store page performance of every app of the organization over a date range, with the previous period.
/// </summary>
public class StoreMetricsSummaryDto
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
    /// Totals of every app.
    /// </summary>
    public StoreMetricValuesDto Totals { get; set; }

    /// <summary>
    /// Change of the totals against the previous period.
    /// </summary>
    public StoreMetricChangesDto Changes { get; set; }

    /// <summary>
    /// Apps with data in either period, most downloads first.
    /// </summary>
    public List<AppStoreMetricsDto> Apps { get; set; } = [];
}

/// <summary>
/// One app of <see cref="StoreMetricsSummaryDto"/>.
/// </summary>
public class AppStoreMetricsDto
{
    /// <summary>
    /// App.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Totals of the range.
    /// </summary>
    public StoreMetricValuesDto Totals { get; set; }

    /// <summary>
    /// Change against the previous period.
    /// </summary>
    public StoreMetricChangesDto Changes { get; set; }
}
