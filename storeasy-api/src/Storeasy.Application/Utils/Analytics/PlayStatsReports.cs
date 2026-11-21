namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// The Google Play Console statistics files storeasy reads from the developer's Cloud Storage bucket
/// (<c>gs://pubsite_prod_rev_...</c>), one file per month and breakdown, UTF-16 comma separated with a header row:
/// <list type="bullet">
/// <item><c>stats/store_performance/store_performance_{package}_{yyyyMM}_country.csv</c>: Date, Package name, Country,
/// Store listing acquisitions, Store listing visitors, Store listing conversion rate. The totals come from here.</item>
/// <item><c>stats/store_performance/store_performance_{package}_{yyyyMM}_traffic_source.csv</c>: Date, Package name,
/// Traffic source, Search term, UTM source, UTM campaign and the same metrics. Rows are summed per day and source and
/// only fill the source slices.</item>
/// </list>
/// Store listing visitors are page views, store listing acquisitions are downloads; Play reports no impressions.
/// </summary>
public static class PlayStatsReports
{
    /// <summary>
    /// Folder of the store performance files in the bucket.
    /// </summary>
    public const string Folder = "stats/store_performance/";

    /// <summary>
    /// Object name of the country file of a month.
    /// </summary>
    /// <param name="packageName"></param>
    /// <param name="month">Any day of the month.</param>
    /// <returns></returns>
    public static string CountryFile(string packageName, DateOnly month) => $"{Folder}store_performance_{packageName}_{month:yyyyMM}_country.csv";

    /// <summary>
    /// Object name of the traffic source file of a month.
    /// </summary>
    /// <param name="packageName"></param>
    /// <param name="month">Any day of the month.</param>
    /// <returns></returns>
    public static string TrafficSourceFile(string packageName, DateOnly month) => $"{Folder}store_performance_{packageName}_{month:yyyyMM}_traffic_source.csv";

    /// <summary>
    /// The first day of every month from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <returns></returns>
    public static List<DateOnly> Months(DateOnly from, DateOnly to)
    {
        var months = new List<DateOnly>();

        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            months.Add(month);

        return months;
    }

    /// <summary>
    /// Maps a Play traffic source.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TrafficSource Source(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "google play search" => TrafficSource.Search,
        "google play explore" => TrafficSource.Browse,
        "third-party referrals" or "third party referrals" => TrafficSource.Referral,
        "" => TrafficSource.Unavailable,
        _ => TrafficSource.Other,
    };

    /// <summary>
    /// Adds a country file to <paramref name="accumulator"/> (country slices and the totals).
    /// </summary>
    /// <param name="text"></param>
    /// <param name="accumulator"></param>
    public static void AddCountryFile(string text, StoreMetricAccumulator accumulator)
    {
        var file = DelimitedText.Parse(text);
        var country = file.ColumnContaining("Country");

        Add(file, accumulator, row => (DelimitedText.Cell(row, country) ?? string.Empty, null), includeInTotal: true);
    }

    /// <summary>
    /// Adds a traffic source file to <paramref name="accumulator"/> (source slices only).
    /// </summary>
    /// <param name="text"></param>
    /// <param name="accumulator"></param>
    public static void AddTrafficSourceFile(string text, StoreMetricAccumulator accumulator)
    {
        var file = DelimitedText.Parse(text);
        var source = file.Column("Traffic source");

        Add(file, accumulator, row => (null, Source(DelimitedText.Cell(row, source))), includeInTotal: false);
    }

    private static void Add(DelimitedText file, StoreMetricAccumulator accumulator, Func<List<string>, (string Country, TrafficSource? Source)> dimension, bool includeInTotal)
    {
        if (file.Rows.Count == 0)
            return;

        var date = file.Column("Date");
        var visitors = file.ColumnContaining("visitors");
        var acquisitions = file.ColumnContaining("acquisitions");

        if (date < 0 || (visitors < 0 && acquisitions < 0))
            throw new FormatException("The Play store performance file has no Date, visitors or acquisitions column.");

        foreach (var row in file.Rows)
        {
            var day = DelimitedText.Date(row, date);

            if (day == null)
                continue;

            var (country, source) = dimension(row);
            var visitorCount = DelimitedText.Count(row, visitors);
            var acquisitionCount = DelimitedText.Count(row, acquisitions);

            if (visitorCount != null)
                accumulator.Add(day.Value, country, source, StoreMetricKind.PageViews, visitorCount.Value, includeInTotal);

            if (acquisitionCount != null)
                accumulator.Add(day.Value, country, source, StoreMetricKind.Downloads, acquisitionCount.Value, includeInTotal);
        }
    }
}
