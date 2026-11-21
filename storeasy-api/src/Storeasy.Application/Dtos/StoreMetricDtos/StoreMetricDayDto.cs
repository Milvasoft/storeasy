namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// One day of the chart.
/// </summary>
public class StoreMetricDayDto : StoreMetricValuesDto
{
    /// <summary>
    /// Day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Maps a summed day.
    /// </summary>
    /// <param name="day"></param>
    /// <returns></returns>
    public static StoreMetricDayDto From(StoreMetricDay day) => new()
    {
        Date = day.Date,
        Impressions = day.Values.Impressions,
        PageViews = day.Values.PageViews,
        Downloads = day.Values.Downloads,
        Redownloads = day.Values.Redownloads,
        ConversionRate = day.Values.ConversionRate,
    };
}
