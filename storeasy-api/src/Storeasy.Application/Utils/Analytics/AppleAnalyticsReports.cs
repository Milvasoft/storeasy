namespace Storeasy.Application.Utils.Analytics;

/// <summary>
/// The App Store Connect analytics reports storeasy reads (Analytics Reports API, standard content level, daily
/// granularity) and their parsers. Files are tab separated with a header row.
/// <list type="bullet">
/// <item>"App Store Discovery and Engagement Standard" (category <c>APP_STORE_ENGAGEMENT</c>): <c>Event</c> Impression
/// counts as impressions, <c>Event</c> Page view on a product page or store sheet as page views; by <c>Source Type</c>
/// and <c>Territory</c>, summing <c>Counts</c>.</item>
/// <item>"App Downloads Standard" (category <c>COMMERCE</c>): <c>Download Type</c> First-time download counts as downloads,
/// Redownload as redownloads (updates and restores are left out); by <c>Source Type</c> and <c>Territory</c>.</item>
/// </list>
/// </summary>
public static class AppleAnalyticsReports
{
    /// <summary>
    /// Name of the discovery and engagement report.
    /// </summary>
    public const string EngagementReportName = "App Store Discovery and Engagement Standard";

    /// <summary>
    /// Name of the downloads report.
    /// </summary>
    public const string DownloadsReportName = "App Downloads Standard";

    /// <summary>
    /// Maps an App Store <c>Source Type</c>.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static TrafficSource Source(string value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "app store search" => TrafficSource.Search,
        "app store browse" => TrafficSource.Browse,
        "app referrer" => TrafficSource.AppReferrer,
        "web referrer" => TrafficSource.WebReferrer,
        "app clip" => TrafficSource.AppClip,
        "notification" => TrafficSource.Notification,
        "unavailable" or "" => TrafficSource.Unavailable,
        _ => TrafficSource.Other,
    };

    /// <summary>
    /// Parses a discovery and engagement report file into slices.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static List<StoreMetricRow> ParseEngagement(string text)
    {
        var file = DelimitedText.Parse(text);
        var date = Required(file, "Date");
        var eventColumn = Required(file, "Event");
        var pageType = file.Column("Page Type");
        var source = file.Column("Source Type");
        var territory = file.Column("Territory");
        var counts = Required(file, "Counts");
        var accumulator = new StoreMetricAccumulator();

        foreach (var row in file.Rows)
        {
            var day = DelimitedText.Date(row, date);
            var count = DelimitedText.Count(row, counts);

            if (day == null || count == null)
                continue;

            var metric = (DelimitedText.Cell(row, eventColumn) ?? string.Empty).ToLowerInvariant() switch
            {
                "impression" => StoreMetricKind.Impressions,
                "page view" when IsStorePage(DelimitedText.Cell(row, pageType)) => StoreMetricKind.PageViews,
                _ => (StoreMetricKind?)null,
            };

            if (metric != null)
                accumulator.Add(day.Value, Territory(row, territory), Source(DelimitedText.Cell(row, source)), metric.Value, count.Value);
        }

        return accumulator.ToRows();
    }

    /// <summary>
    /// Parses a downloads report file into slices.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static List<StoreMetricRow> ParseDownloads(string text)
    {
        var file = DelimitedText.Parse(text);
        var date = Required(file, "Date");
        var type = Required(file, "Download Type");
        var source = file.Column("Source Type");
        var territory = file.Column("Territory");
        var counts = Required(file, "Counts");
        var accumulator = new StoreMetricAccumulator();

        foreach (var row in file.Rows)
        {
            var day = DelimitedText.Date(row, date);
            var count = DelimitedText.Count(row, counts);

            if (day == null || count == null)
                continue;

            var metric = (DelimitedText.Cell(row, type) ?? string.Empty).ToLowerInvariant() switch
            {
                "first-time download" or "first time download" => StoreMetricKind.Downloads,
                "redownload" => StoreMetricKind.Redownloads,
                _ => (StoreMetricKind?)null,
            };

            if (metric != null)
                accumulator.Add(day.Value, Territory(row, territory), Source(DelimitedText.Cell(row, source)), metric.Value, count.Value);
        }

        return accumulator.ToRows();
    }

    // A page view counts when it is the app's own page: the product page, or the product page shown as a store sheet in
    // another app. Version history, privacy and developer pages are not store page views. A missing page type counts.
    private static bool IsStorePage(string pageType) => string.IsNullOrEmpty(pageType)
                                                        || pageType.Equals("Product page", StringComparison.OrdinalIgnoreCase)
                                                        || pageType.Equals("Store sheet", StringComparison.OrdinalIgnoreCase);

    private static string Territory(List<string> row, int column) => column < 0 ? null : DelimitedText.Cell(row, column) ?? string.Empty;

    private static int Required(DelimitedText file, string name)
    {
        var column = file.Column(name);

        return column >= 0 || file.Rows.Count == 0 ? column : throw new FormatException($"The report has no '{name}' column.");
    }
}
