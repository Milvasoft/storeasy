using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Infrastructure.Persistence.Context;

namespace Storeasy.Infrastructure.Aso.Impact;

/// <summary>
/// <see cref="IAsoImpactAnalyzer"/>: merges every registered <see cref="IImpactEventSource"/> and computes a report with
/// one daily metrics read over both windows (<see cref="IStoreMetricsQuery"/>) and the tracked keywords' rank series
/// (<see cref="IAsoRankSeries"/>); the arithmetic is <see cref="ImpactMath"/>. Nothing calls a store.
/// </summary>
/// <param name="sources"></param>
/// <param name="metricsQuery"></param>
/// <param name="rankSeries"></param>
/// <param name="dbContext"></param>
public sealed class AsoImpactAnalyzer(IEnumerable<IImpactEventSource> sources,
                                      IStoreMetricsQuery metricsQuery,
                                      IAsoRankSeries rankSeries,
                                      StoreasyDbContext dbContext) : IAsoImpactAnalyzer
{
    /// <summary>
    /// Most tracked keywords compared in one report.
    /// </summary>
    public const int MaxKeywords = 100;

    private readonly IReadOnlyList<IImpactEventSource> _sources = [.. sources];
    private readonly IStoreMetricsQuery _metricsQuery = metricsQuery;
    private readonly IAsoRankSeries _rankSeries = rankSeries;
    private readonly StoreasyDbContext _dbContext = dbContext;

    /// <inheritdoc/>
    public async Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken)
    {
        var lists = new List<List<ImpactEvent>>();

        // One after the other: the sources share the request's DbContext.
        foreach (var source in _sources)
            lists.Add(await source.GetEventsAsync(query, cancellationToken));

        return ImpactEventRules.Merge(lists);
    }

    /// <inheritdoc/>
    public async Task<AsoImpactReportDto> AnalyzeAsync(ImpactEvent impactEvent, IReadOnlyList<ImpactEvent> others, ImpactOptions options, bool includeRanks, CancellationToken cancellationToken)
    {
        var windows = ImpactMath.Windows(impactEvent.Date, options.WindowDays, options.GapDays);
        var scope = new StoreMetricScope(impactEvent.OrganizationId, impactEvent.AppId, impactEvent.StoreAppId, impactEvent.Store, options.Country, options.Source);
        var days = await _metricsQuery.GetDailyAsync(scope, windows.BeforeFrom, windows.AfterTo, cancellationToken);
        var comparison = ImpactMath.CompareMetrics(days, windows);
        var (confounders, related) = ImpactMath.Overlapping(impactEvent, others, windows);

        return new AsoImpactReportDto
        {
            Event = AsoImpactEventDto.From(impactEvent),
            Status = ImpactMath.Status(impactEvent.DateSource, windows, comparison, options.Today),
            WindowDays = windows.WindowDays,
            GapDays = windows.GapDays,
            WholeWeeks = windows.WholeWeeks,
            WeekdayMatched = comparison.WeekdayMatched,
            Before = new AsoImpactWindowDto { From = windows.BeforeFrom, To = windows.BeforeTo, DaysWithData = comparison.BeforeDaysWithData },
            After = new AsoImpactWindowDto { From = windows.AfterFrom, To = windows.AfterTo, DaysWithData = comparison.AfterDaysWithData },
            ReadyDate = windows.AfterTo.AddDays(1),
            Country = string.IsNullOrWhiteSpace(options.Country) ? null : options.Country.Trim().ToUpperInvariant(),
            Source = options.Source,
            Metrics = comparison.Metrics,
            Conversion = comparison.Conversion,
            BeforeDays = comparison.BeforeDays,
            AfterDays = comparison.AfterDays,
            Ranks = includeRanks ? await RanksAsync(impactEvent, windows, options.Country, cancellationToken) : null,
            Confounders = [.. confounders.Select(e => AsoImpactEventDto.From(e))],
            Related = [.. related.Select(e => AsoImpactEventDto.From(e))],
        };
    }

    private async Task<AsoImpactRanksDto> RanksAsync(ImpactEvent impactEvent, ImpactWindows windows, string country, CancellationToken cancellationToken)
    {
        var keywordsQuery = _dbContext.AsoTrackedKeywords.AsNoTracking().Where(t => t.OrganizationId == impactEvent.OrganizationId && t.StoreAppId == impactEvent.StoreAppId);

        if (!string.IsNullOrWhiteSpace(country))
        {
            var lower = country.Trim().ToLowerInvariant();

            keywordsQuery = keywordsQuery.Where(t => t.Country == lower);
        }

        var keywords = await keywordsQuery.OrderByDescending(t => t.IsActive)
                                          .ThenBy(t => t.Keyword)
                                          .ThenBy(t => t.Country)
                                          .Select(t => new { t.Keyword, t.Country })
                                          .Take(MaxKeywords)
                                          .ToListAsync(cancellationToken);

        var series = new List<ImpactKeywordSeries>();

        foreach (var keyword in keywords)
        {
            var points = await _rankSeries.GetRankSeriesAsync(impactEvent.StoreAppId, keyword.Keyword, keyword.Country, windows.BeforeFrom, windows.AfterTo, cancellationToken);

            series.Add(new ImpactKeywordSeries(keyword.Keyword, keyword.Country, points.Points));
        }

        return ImpactMath.CompareRanks(series, windows);
    }
}
