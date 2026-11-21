namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// Combines rows read from several reports or report instances.
/// </summary>
public static class StoreMetricRows
{
    /// <summary>
    /// Merges rows of the same day, country and source: a metric one row reports fills the gap of another, and a later
    /// row's reported value wins (two reports of one store each carry their own metrics, e.g. the App Store's discovery
    /// and downloads reports).
    /// </summary>
    /// <param name="rows"></param>
    /// <returns></returns>
    public static List<StoreMetricRow> Merge(IEnumerable<StoreMetricRow> rows)
    {
        var merged = new Dictionary<(DateOnly, string, TrafficSource), StoreMetricRow>();

        foreach (var row in rows)
        {
            var key = (row.Date, row.Country, row.Source);

            merged[key] = merged.TryGetValue(key, out var existing)
                ? existing with
                {
                    Impressions = row.Impressions ?? existing.Impressions,
                    PageViews = row.PageViews ?? existing.PageViews,
                    Downloads = row.Downloads ?? existing.Downloads,
                    Redownloads = row.Redownloads ?? existing.Redownloads,
                }
                : row;
        }

        return [.. merged.Values.OrderBy(r => r.Date).ThenBy(r => r.Country, StringComparer.Ordinal).ThenBy(r => r.Source)];
    }

    /// <summary>
    /// Keeps, for every day, the rows of the last batch that contains the day. Report instances of the App Store carry
    /// complete data for the days they hold, so a later instance replaces a day instead of adding to it.
    /// </summary>
    /// <param name="batches">Batches in delivery order (oldest first).</param>
    /// <returns></returns>
    public static List<StoreMetricRow> LatestPerDay(IEnumerable<IReadOnlyList<StoreMetricRow>> batches)
    {
        var days = new Dictionary<DateOnly, List<StoreMetricRow>>();

        foreach (var batch in batches)
        {
            foreach (var day in batch.GroupBy(r => r.Date))
                days[day.Key] = [.. day];
        }

        return [.. days.OrderBy(d => d.Key).SelectMany(d => d.Value)];
    }
}
