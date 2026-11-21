using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads the stored daily store page metrics for any feature (the metrics page, MCP tools, a before and after impact
/// report). Every method reads exactly one slice of the rows (see <see cref="Domain.Analytics.StoreMetric"/>): the
/// scope's country (or every country) and source (or every source), summed over the store records the scope selects.
/// Nothing calls a store. Dates are the stores' reporting days; ranges include both ends.
/// </summary>
public interface IStoreMetricsQuery
{
    /// <summary>
    /// One value per day with data for <paramref name="metric"/>: <c>GetMetricSeries(appId, metric, from, to, filters)</c>.
    /// Days without rows are left out; <see cref="StoreMetricKind.ConversionRate"/> is computed per day.
    /// </summary>
    Task<List<StoreMetricPoint>> GetMetricSeriesAsync(StoreMetricScope scope, StoreMetricKind metric, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Every metric per day with data, oldest first.
    /// </summary>
    Task<List<StoreMetricDay>> GetDailyAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Every metric summed over the range.
    /// </summary>
    Task<StoreMetricValues> GetTotalsAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Totals per traffic source (the scope's source is ignored), largest page views first.
    /// </summary>
    Task<List<StoreMetricGroup<TrafficSource>>> GetBySourceAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Totals per country (the scope's country is ignored), most downloads first, at most <paramref name="limit"/>.
    /// </summary>
    Task<List<StoreMetricGroup<string>>> GetByCountryAsync(StoreMetricScope scope, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Totals per store (the scope's store is ignored).
    /// </summary>
    Task<List<StoreMetricGroup<StoreKind>>> GetByStoreAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>
    /// Totals per storeasy app of the organization (the scope's app and record are ignored).
    /// </summary>
    Task<List<StoreMetricGroup<int>>> GetByAppAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

/// <summary>
/// Which rows a metrics read sums.
/// </summary>
public sealed record StoreMetricScope
{
    /// <summary>
    /// A scope.
    /// </summary>
    /// <param name="organizationId">Organization (always checked).</param>
    /// <param name="appId">Only the records of this app; null for every app of the organization.</param>
    /// <param name="storeAppId">Only this store record.</param>
    /// <param name="store">Only this store.</param>
    /// <param name="country">Only this country (ISO code); null or empty for every country.</param>
    /// <param name="source">Only this traffic source; null or <see cref="TrafficSource.All"/> for every source.</param>
    public StoreMetricScope(int organizationId, int? appId = null, int? storeAppId = null, StoreKind? store = null, string country = null, TrafficSource? source = null)
    {
        OrganizationId = organizationId;
        AppId = appId;
        StoreAppId = storeAppId;
        Store = store;
        Country = country;
        Source = source;
    }

    /// <summary>
    /// Organization (always checked).
    /// </summary>
    public int OrganizationId { get; init; }

    /// <summary>
    /// Only the records of this app; null for every app of the organization.
    /// </summary>
    public int? AppId { get; init; }

    /// <summary>
    /// Only this store record.
    /// </summary>
    public int? StoreAppId { get; init; }

    /// <summary>
    /// Only this store.
    /// </summary>
    public StoreKind? Store { get; init; }

    /// <summary>
    /// Only this country (ISO code); null or empty for every country.
    /// </summary>
    public string Country { get; init; }

    /// <summary>
    /// Only this traffic source; null or <see cref="TrafficSource.All"/> for every source.
    /// </summary>
    public TrafficSource? Source { get; init; }

    /// <summary>
    /// The country slice the scope reads.
    /// </summary>
    public string CountrySlice => string.IsNullOrWhiteSpace(Country) ? StoreMetricRules.AllCountries : Country.Trim().ToUpperInvariant();

    /// <summary>
    /// The source slice the scope reads.
    /// </summary>
    public TrafficSource SourceSlice => Source ?? TrafficSource.All;
}

/// <summary>
/// Summed metrics. <see cref="ConvertingDownloads"/> are the downloads of the rows that also report page views, so the
/// conversion rate never divides one store's downloads by another store's page views.
/// </summary>
public sealed record StoreMetricValues
{
    /// <summary>
    /// Summed metrics.
    /// </summary>
    /// <param name="impressions">Impressions, null when no row reports them.</param>
    /// <param name="pageViews">Page views, null when no row reports them.</param>
    /// <param name="downloads">First time downloads, null when no row reports them.</param>
    /// <param name="redownloads">Redownloads, null when no row reports them.</param>
    /// <param name="convertingDownloads">Downloads of the rows that report page views.</param>
    public StoreMetricValues(long? impressions, long? pageViews, long? downloads, long? redownloads, long? convertingDownloads)
    {
        Impressions = impressions;
        PageViews = pageViews;
        Downloads = downloads;
        Redownloads = redownloads;
        ConvertingDownloads = convertingDownloads;
    }

    /// <summary>
    /// Impressions, null when no row reports them.
    /// </summary>
    public long? Impressions { get; init; }

    /// <summary>
    /// Page views, null when no row reports them.
    /// </summary>
    public long? PageViews { get; init; }

    /// <summary>
    /// First time downloads, null when no row reports them.
    /// </summary>
    public long? Downloads { get; init; }

    /// <summary>
    /// Redownloads, null when no row reports them.
    /// </summary>
    public long? Redownloads { get; init; }

    /// <summary>
    /// Downloads of the rows that report page views.
    /// </summary>
    public long? ConvertingDownloads { get; init; }

    /// <summary>
    /// Nothing.
    /// </summary>
    public static StoreMetricValues Empty { get; } = new(null, null, null, null, null);

    /// <summary>
    /// Downloads divided by page views, or null.
    /// </summary>
    public double? ConversionRate => StoreMetricRules.ConversionRate(ConvertingDownloads, PageViews);

    /// <summary>
    /// The value of one metric.
    /// </summary>
    /// <param name="metric"></param>
    /// <returns></returns>
    public double? Value(StoreMetricKind metric) => metric switch
    {
        StoreMetricKind.Impressions => Impressions,
        StoreMetricKind.PageViews => PageViews,
        StoreMetricKind.Downloads => Downloads,
        StoreMetricKind.Redownloads => Redownloads,
        StoreMetricKind.ConversionRate => ConversionRate,
        _ => null,
    };
}

/// <summary>
/// One day of summed metrics.
/// </summary>
/// <param name="Date">Day.</param>
/// <param name="Values">Metrics.</param>
public sealed record StoreMetricDay(DateOnly Date, StoreMetricValues Values);

/// <summary>
/// One day of one metric.
/// </summary>
/// <param name="Date">Day.</param>
/// <param name="Value">Value, null when no row of the day reports the metric.</param>
public sealed record StoreMetricPoint(DateOnly Date, double? Value);

/// <summary>
/// Summed metrics of one group (source, country, store or app).
/// </summary>
/// <typeparam name="TKey"></typeparam>
/// <param name="Key">Group.</param>
/// <param name="Values">Metrics.</param>
public sealed record StoreMetricGroup<TKey>(TKey Key, StoreMetricValues Values);
