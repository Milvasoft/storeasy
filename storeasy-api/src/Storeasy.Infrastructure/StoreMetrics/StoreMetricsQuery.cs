using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Persistence.Context;
using System.Linq.Expressions;

namespace Storeasy.Infrastructure.StoreMetrics;

/// <summary>
/// <see cref="IStoreMetricsQuery"/> over the <c>StoreMetrics</c> table: every read is one grouped SQL query over one
/// slice. A metric is null in a group when none of its rows reports it (SQL sums would read 0). Rows of removed apps are
/// left out.
/// </summary>
/// <param name="dbContext"></param>
public sealed class StoreMetricsQuery(StoreasyDbContext dbContext) : IStoreMetricsQuery
{
    private readonly StoreasyDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<List<StoreMetricPoint>> GetMetricSeriesAsync(StoreMetricScope scope, StoreMetricKind metric, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => [.. (await GetDailyAsync(scope, from, to, cancellationToken)).Select(d => new StoreMetricPoint(d.Date, d.Values.Value(metric)))];

    /// <inheritdoc/>
    public async Task<List<StoreMetricDay>> GetDailyAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => [.. (await GroupAsync(Rows(scope, from, to, false, false), m => m.Date, cancellationToken)).OrderBy(g => g.Key).Select(g => new StoreMetricDay(g.Key, g.Values))];

    /// <inheritdoc/>
    public async Task<StoreMetricValues> GetTotalsAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => (await GroupAsync(Rows(scope, from, to, false, false), m => 1, cancellationToken)).FirstOrDefault()?.Values ?? StoreMetricValues.Empty;

    /// <inheritdoc/>
    public async Task<List<StoreMetricGroup<TrafficSource>>> GetBySourceAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => [.. (await GroupAsync(Rows(scope, from, to, false, true), m => m.Source, cancellationToken)).OrderByDescending(g => g.Values.PageViews ?? 0)
                                                                                                         .ThenByDescending(g => g.Values.Downloads ?? 0)
                                                                                                         .ThenBy(g => g.Key)];

    /// <inheritdoc/>
    public async Task<List<StoreMetricGroup<string>>> GetByCountryAsync(StoreMetricScope scope, DateOnly from, DateOnly to, int limit, CancellationToken cancellationToken)
        => [.. (await GroupAsync(Rows(scope, from, to, true, false), m => m.Country, cancellationToken)).OrderByDescending(g => g.Values.Downloads ?? 0)
                                                                                                          .ThenByDescending(g => g.Values.PageViews ?? 0)
                                                                                                          .ThenBy(g => g.Key, StringComparer.Ordinal)
                                                                                                          .Take(limit)];

    /// <inheritdoc/>
    public async Task<List<StoreMetricGroup<StoreKind>>> GetByStoreAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => [.. (await GroupAsync(Rows(scope with { Store = null }, from, to, false, false), m => m.Store, cancellationToken)).OrderBy(g => g.Key)];

    /// <inheritdoc/>
    public Task<List<StoreMetricGroup<int>>> GetByAppAsync(StoreMetricScope scope, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => GroupAsync(Rows(scope with { AppId = null, StoreAppId = null }, from, to, false, false), m => m.StoreApp.AppId, cancellationToken);

    private IQueryable<StoreMetric> Rows(StoreMetricScope scope, DateOnly from, DateOnly to, bool byCountry, bool bySource)
    {
        var country = scope.CountrySlice;
        var source = scope.SourceSlice;
        var rows = _dbContext.StoreMetrics.Where(m => m.OrganizationId == scope.OrganizationId && m.Date >= from && m.Date <= to && !m.StoreApp.App.IsDeleted);

        if (scope.AppId.HasValue)
            rows = rows.Where(m => m.StoreApp.AppId == scope.AppId.Value);

        if (scope.StoreAppId.HasValue)
            rows = rows.Where(m => m.StoreAppId == scope.StoreAppId.Value);

        if (scope.Store.HasValue)
            rows = rows.Where(m => m.Store == scope.Store.Value);

        rows = byCountry ? rows.Where(m => m.Country != StoreMetricRules.AllCountries) : rows.Where(m => m.Country == country);

        return bySource ? rows.Where(m => m.Source != TrafficSource.All) : rows.Where(m => m.Source == source);
    }

    private static async Task<List<StoreMetricGroup<TKey>>> GroupAsync<TKey>(IQueryable<StoreMetric> rows, Expression<Func<StoreMetric, TKey>> key, CancellationToken cancellationToken)
    {
        var sums = await rows.GroupBy(key)
                             .Select(g => new MetricSums<TKey>
                             {
                                 Key = g.Key,
                                 Impressions = g.Sum(m => m.Impressions) ?? 0,
                                 ImpressionRows = g.Count(m => m.Impressions != null),
                                 PageViews = g.Sum(m => m.PageViews) ?? 0,
                                 PageViewRows = g.Count(m => m.PageViews != null),
                                 Downloads = g.Sum(m => m.Downloads) ?? 0,
                                 DownloadRows = g.Count(m => m.Downloads != null),
                                 Redownloads = g.Sum(m => m.Redownloads) ?? 0,
                                 RedownloadRows = g.Count(m => m.Redownloads != null),
                                 ConvertingDownloads = g.Sum(m => m.PageViews != null ? m.Downloads : null) ?? 0,
                                 ConvertingRows = g.Count(m => m.PageViews != null && m.Downloads != null),
                             })
                             .ToListAsync(cancellationToken);

        return [.. sums.Select(s => new StoreMetricGroup<TKey>(s.Key, new StoreMetricValues(s.ImpressionRows > 0 ? s.Impressions : null,
                                                                                         s.PageViewRows > 0 ? s.PageViews : null,
                                                                                         s.DownloadRows > 0 ? s.Downloads : null,
                                                                                         s.RedownloadRows > 0 ? s.Redownloads : null,
                                                                                         s.ConvertingRows > 0 ? s.ConvertingDownloads : null)))];
    }

    /// <summary>
    /// Sums of one group as SQL returns them, with the number of rows that report each metric.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    private sealed class MetricSums<TKey>
    {
        public TKey Key { get; set; }
        public long Impressions { get; set; }
        public int ImpressionRows { get; set; }
        public long PageViews { get; set; }
        public int PageViewRows { get; set; }
        public long Downloads { get; set; }
        public int DownloadRows { get; set; }
        public long Redownloads { get; set; }
        public int RedownloadRows { get; set; }
        public long ConvertingDownloads { get; set; }
        public int ConvertingRows { get; set; }
    }
}
