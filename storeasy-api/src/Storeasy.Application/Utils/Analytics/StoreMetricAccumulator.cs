using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// Sums store report facts into the daily slices of <see cref="StoreMetric"/>. A fact names its country and source when
/// the report has that dimension (null when it does not); it is added to its own slice and to every coarser one: a fact
/// of (TR, search) counts for (TR, search), (TR, all), (all, search) and (all, all). A fact that only a breakdown file
/// carries (Google Play's traffic source file, while the country file holds the totals) is added with
/// <c>includeInTotal: false</c>, so the total is not counted twice.
/// </summary>
public sealed class StoreMetricAccumulator
{
    private readonly Dictionary<(DateOnly Date, string Country, TrafficSource Source), long?[]> _slices = [];

    /// <summary>
    /// Number of slices so far.
    /// </summary>
    public int Count => _slices.Count;

    /// <summary>
    /// Adds a fact.
    /// </summary>
    /// <param name="date">Day.</param>
    /// <param name="country">Country code; null when the report has no country dimension.</param>
    /// <param name="source">Traffic source; null when the report has no source dimension.</param>
    /// <param name="metric">One of the stored metrics (not <see cref="StoreMetricKind.ConversionRate"/>).</param>
    /// <param name="value">Count.</param>
    /// <param name="includeInTotal">Whether the fact also counts for the (all countries, all sources) slice.</param>
    public void Add(DateOnly date, string country, TrafficSource? source, StoreMetricKind metric, long value, bool includeInTotal = true)
    {
        var index = Index(metric);
        var countries = country == null ? [StoreMetricRules.AllCountries] : new[] { StoreMetricRules.NormalizeCountry(country), StoreMetricRules.AllCountries };
        var sources = source is null or TrafficSource.All ? [TrafficSource.All] : new[] { source.Value, TrafficSource.All };

        foreach (var sliceCountry in countries)
        {
            foreach (var sliceSource in sources)
            {
                if (!includeInTotal && sliceCountry == StoreMetricRules.AllCountries && sliceSource == TrafficSource.All)
                    continue;

                var key = (date, sliceCountry, sliceSource);

                if (!_slices.TryGetValue(key, out var values))
                {
                    values = new long?[4];
                    _slices[key] = values;
                }

                values[index] = (values[index] ?? 0) + value;
            }
        }
    }

    /// <summary>
    /// Marks a metric as reported (zero) for a slice without adding to it, so a day the store reported as empty reads 0
    /// instead of "not reported".
    /// </summary>
    /// <param name="date"></param>
    /// <param name="country"></param>
    /// <param name="source"></param>
    /// <param name="metric"></param>
    /// <param name="includeInTotal"></param>
    public void Touch(DateOnly date, string country, TrafficSource? source, StoreMetricKind metric, bool includeInTotal = true)
        => Add(date, country, source, metric, 0, includeInTotal);

    /// <summary>
    /// The slices as rows, ordered by day, country and source.
    /// </summary>
    /// <returns></returns>
    public List<StoreMetricRow> ToRows()
        => [.. _slices.OrderBy(s => s.Key.Date)
                      .ThenBy(s => s.Key.Country, StringComparer.Ordinal)
                      .ThenBy(s => s.Key.Source)
                      .Select(s => new StoreMetricRow(s.Key.Date, s.Key.Country, s.Key.Source, s.Value[0], s.Value[1], s.Value[2], s.Value[3]))];

    private static int Index(StoreMetricKind metric) => metric switch
    {
        StoreMetricKind.Impressions => 0,
        StoreMetricKind.PageViews => 1,
        StoreMetricKind.Downloads => 2,
        StoreMetricKind.Redownloads => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Only stored metrics can be added."),
    };
}
