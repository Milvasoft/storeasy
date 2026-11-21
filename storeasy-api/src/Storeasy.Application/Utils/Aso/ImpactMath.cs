using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Analytics;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// The arithmetic of the before and after impact report, without any I/O (docs/aso-impact.md):
/// <list type="bullet">
/// <item>Windows: the before window is the <c>W</c> days ending the day before the effect day; the after window is the
/// <c>W</c> days starting <c>gap</c> days after the effect day (the default gap of 2 skips the effect day and the next,
/// because stores apply changes with a delay).</item>
/// <item>Counts are compared as daily averages over the weekdays both windows have data for. With whole week windows
/// (<c>W</c> a multiple of 7) and no missing day this is the plain daily average; otherwise a weekday present in one window
/// only is left out of both, so a window with more weekend days does not look better or worse.</item>
/// <item>The conversion rate is total downloads over total page views of each window; its change is checked with a two
/// proportion z-test. Below <see cref="MinPageViews"/> page views in a window or <see cref="MinDownloads"/> downloads in
/// both, the signal is "not enough data".</item>
/// <item>Ranks are averaged over the checked days the app was in the results; a keyword ranked in one window only counts
/// as improved (newly ranked) or declined (dropped out).</item>
/// </list>
/// </summary>
public static class ImpactMath
{
    /// <summary>
    /// Fewest page views per window for the conversion test.
    /// </summary>
    public const long MinPageViews = 100;

    /// <summary>
    /// Fewest downloads of both windows together for the conversion test.
    /// </summary>
    public const long MinDownloads = 20;

    /// <summary>
    /// p-value below which a change is significant.
    /// </summary>
    public const double SignificantP = 0.05;

    /// <summary>
    /// p-value below which a change is suggestive.
    /// </summary>
    public const double SuggestiveP = 0.10;

    /// <summary>
    /// A keyword whose average rank is at most this is in the top 10.
    /// </summary>
    public const double TopRank = 10;

    /// <summary>
    /// Smallest average rank move counted as improved or declined.
    /// </summary>
    public const double RankMove = 1;

    /// <summary>
    /// The two windows around an effect day.
    /// </summary>
    /// <param name="effectDay">Day the change took effect.</param>
    /// <param name="windowDays">Length of each window (at least 1).</param>
    /// <param name="gapDays">Days skipped from the effect day on (0 starts the after window on the effect day).</param>
    /// <returns></returns>
    public static ImpactWindows Windows(DateOnly effectDay, int windowDays, int gapDays)
    {
        var window = Math.Max(1, windowDays);
        var gap = Math.Max(0, gapDays);

        return new ImpactWindows(effectDay.AddDays(-window), effectDay.AddDays(-1), effectDay.AddDays(gap), effectDay.AddDays(gap + window - 1), window, gap);
    }

    /// <summary>
    /// Compares the store page metrics of the two windows. <paramref name="days"/> may hold days outside the windows; they
    /// are ignored.
    /// </summary>
    /// <param name="days"></param>
    /// <param name="windows"></param>
    /// <returns></returns>
    public static ImpactMetricsComparison CompareMetrics(IReadOnlyList<StoreMetricDay> days, ImpactWindows windows)
    {
        var byDay = days.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Last().Values);
        var before = WindowDays(byDay, windows.BeforeFrom, windows.WindowDays);
        var after = WindowDays(byDay, windows.AfterFrom, windows.WindowDays);
        var matched = false;
        var metrics = new List<AsoImpactMetricDto>();

        foreach (var metric in new[] { StoreMetricKind.Impressions, StoreMetricKind.PageViews, StoreMetricKind.Downloads })
        {
            var (comparison, weekdayMatched) = CompareCount(metric, before, after);

            matched |= weekdayMatched;
            metrics.Add(comparison);
        }

        var test = ConversionTest(Converting(before), Viewed(before), Converting(after), Viewed(after));
        var rateBefore = StoreMetricRules.ConversionRate(test.BeforeDownloads, test.BeforePageViews);
        var rateAfter = StoreMetricRules.ConversionRate(test.AfterDownloads, test.AfterPageViews);
        var hasRate = before.Any(d => d.Values?.ConversionRate != null) || after.Any(d => d.Values?.ConversionRate != null);

        metrics.Add(new AsoImpactMetricDto
        {
            Metric = StoreMetricKind.ConversionRate,
            Before = hasRate ? rateBefore : null,
            After = hasRate ? rateAfter : null,
            AbsoluteChange = hasRate && rateBefore.HasValue && rateAfter.HasValue ? rateAfter - rateBefore : null,
            RelativeChange = hasRate ? Relative(rateBefore, rateAfter) : null,
        });

        return new ImpactMetricsComparison
        {
            Metrics = metrics,
            Conversion = test,
            BeforeDaysWithData = before.Count(d => d.Values != null),
            AfterDaysWithData = after.Count(d => d.Values != null),
            WeekdayMatched = matched,
            BeforeDays = [.. before.Select((d, i) => DayDto(i, d))],
            AfterDays = [.. after.Select((d, i) => DayDto(i, d))],
        };
    }

    /// <summary>
    /// Two proportion z-test of <paramref name="afterDownloads"/> / <paramref name="afterViews"/> against
    /// <paramref name="beforeDownloads"/> / <paramref name="beforeViews"/>, with the pooled standard error.
    /// </summary>
    /// <param name="beforeDownloads"></param>
    /// <param name="beforeViews"></param>
    /// <param name="afterDownloads"></param>
    /// <param name="afterViews"></param>
    /// <returns></returns>
    public static AsoImpactConversionTestDto ConversionTest(long beforeDownloads, long beforeViews, long afterDownloads, long afterViews)
    {
        var result = new AsoImpactConversionTestDto
        {
            BeforeDownloads = beforeDownloads,
            BeforePageViews = beforeViews,
            AfterDownloads = afterDownloads,
            AfterPageViews = afterViews,
            Signal = ImpactSignal.InsufficientData,
        };

        if (beforeViews < MinPageViews || afterViews < MinPageViews || beforeDownloads + afterDownloads < MinDownloads)
            return result;

        // App Store downloads include ones without a page view (a direct link, the search result's Get button), so a rate
        // above 1 is possible; it is not a proportion and the test does not apply.
        if (beforeDownloads > beforeViews || afterDownloads > afterViews)
        {
            result.Signal = ImpactSignal.NotApplicable;
            return result;
        }

        var p1 = (double)beforeDownloads / beforeViews;
        var p2 = (double)afterDownloads / afterViews;
        var pooled = (double)(beforeDownloads + afterDownloads) / (beforeViews + afterViews);
        var standardError = Math.Sqrt(pooled * (1 - pooled) * ((1d / beforeViews) + (1d / afterViews)));
        var z = standardError > 0 ? (p2 - p1) / standardError : 0;
        var p = 2 * (1 - NormalCdf(Math.Abs(z)));

        result.ZScore = z;
        result.PValue = Math.Clamp(p, 0, 1);
        result.Signal = p < SignificantP ? ImpactSignal.Significant : p < SuggestiveP ? ImpactSignal.Suggestive : ImpactSignal.NotSignificant;

        return result;
    }

    /// <summary>
    /// Standard normal cumulative distribution (Abramowitz and Stegun 7.1.26, absolute error below 1.5e-7).
    /// </summary>
    /// <param name="z"></param>
    /// <returns></returns>
    public static double NormalCdf(double z)
    {
        var x = Math.Abs(z) / Math.Sqrt(2);
        var t = 1 / (1 + (0.3275911 * x));
        var erf = 1 - (((((((((1.061405429 * t) - 1.453152027) * t) + 1.421413741) * t) - 0.284496736) * t) + 0.254829592) * t * Math.Exp(-x * x));

        return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }

    /// <summary>
    /// The other live events of the same store record from the first day of the before window to the last day of the after
    /// window (the gap included): confounders, unless they went live together with <paramref name="target"/> (same
    /// <see cref="ImpactEvent.ReleaseKey"/>), then related.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="others"></param>
    /// <param name="windows"></param>
    /// <returns></returns>
    public static (List<ImpactEvent> Confounders, List<ImpactEvent> Related) Overlapping(ImpactEvent target, IEnumerable<ImpactEvent> others, ImpactWindows windows)
    {
        var inside = others.Where(e => e.Id != target.Id
                                       && e.StoreAppId == target.StoreAppId
                                       && e.DateSource != ImpactDateSource.AwaitingRelease
                                       && e.Date >= windows.BeforeFrom
                                       && e.Date <= windows.AfterTo)
                           .OrderBy(e => e.Date)
                           .ThenBy(e => e.Id, StringComparer.Ordinal)
                           .ToList();

        bool SameRelease(ImpactEvent e) => target.ReleaseKey != null && e.ReleaseKey == target.ReleaseKey && e.Date == target.Date;

        return ([.. inside.Where(e => !SameRelease(e))], [.. inside.Where(SameRelease)]);
    }

    /// <summary>
    /// Whether a report can be read: not before the change is live, not without data, final once the after window is over.
    /// </summary>
    /// <param name="dateSource"></param>
    /// <param name="windows"></param>
    /// <param name="comparison"></param>
    /// <param name="today">Today (UTC); the stores report a day late at the earliest.</param>
    /// <returns></returns>
    public static ImpactStatus Status(ImpactDateSource dateSource, ImpactWindows windows, ImpactMetricsComparison comparison, DateOnly today)
    {
        if (dateSource == ImpactDateSource.AwaitingRelease)
            return ImpactStatus.AwaitingRelease;

        if (comparison.BeforeDaysWithData == 0 && comparison.AfterDaysWithData == 0)
            return ImpactStatus.NoData;

        return windows.AfterTo >= today ? ImpactStatus.Collecting : ImpactStatus.Ready;
    }

    /// <summary>
    /// Compares tracked keyword ranks. Every series may cover more days than the windows.
    /// </summary>
    /// <param name="series"></param>
    /// <param name="windows"></param>
    /// <returns></returns>
    public static AsoImpactRanksDto CompareRanks(IEnumerable<ImpactKeywordSeries> series, ImpactWindows windows)
    {
        var keywords = new List<AsoImpactKeywordDto>();

        foreach (var item in series)
        {
            var before = item.Points.Where(p => p.Checked && p.Date >= windows.BeforeFrom && p.Date <= windows.BeforeTo).ToList();
            var after = item.Points.Where(p => p.Checked && p.Date >= windows.AfterFrom && p.Date <= windows.AfterTo).ToList();

            if (before.Count == 0 && after.Count == 0)
                continue;

            var beforeRanks = before.Where(p => p.Rank.HasValue).Select(p => (double)p.Rank.Value).ToList();
            var afterRanks = after.Where(p => p.Rank.HasValue).Select(p => (double)p.Rank.Value).ToList();
            double? beforeAverage = beforeRanks.Count > 0 ? beforeRanks.Average() : null;
            double? afterAverage = afterRanks.Count > 0 ? afterRanks.Average() : null;

            keywords.Add(new AsoImpactKeywordDto
            {
                Keyword = item.Keyword,
                Country = item.Country,
                Before = beforeAverage,
                After = afterAverage,
                Change = beforeAverage.HasValue && afterAverage.HasValue ? afterAverage - beforeAverage : null,
                BeforeCheckedDays = before.Count,
                BeforeRankedDays = beforeRanks.Count,
                AfterCheckedDays = after.Count,
                AfterRankedDays = afterRanks.Count,
            });
        }

        var compared = keywords.Where(k => k.Change.HasValue).ToList();

        // A keyword that entered the results counts as improved and one that left them as declined, but only when the
        // other window was checked (a keyword tracked after the change has no "before").
        bool Entered(AsoImpactKeywordDto k) => k.Before == null && k.After != null && k.BeforeCheckedDays > 0;
        bool Dropped(AsoImpactKeywordDto k) => k.Before != null && k.After == null && k.AfterCheckedDays > 0;

        return new AsoImpactRanksDto
        {
            KeywordCount = keywords.Count,
            Compared = compared.Count,
            BeforeAverage = compared.Count > 0 ? compared.Average(k => k.Before.Value) : null,
            AfterAverage = compared.Count > 0 ? compared.Average(k => k.After.Value) : null,
            BeforeTop10 = keywords.Count(k => k.Before <= TopRank && k.BeforeCheckedDays > 0),
            AfterTop10 = keywords.Count(k => k.After <= TopRank && k.AfterCheckedDays > 0),
            Improved = keywords.Count(k => k.Change <= -RankMove || Entered(k)),
            Declined = keywords.Count(k => k.Change >= RankMove || Dropped(k)),
            Keywords = [.. keywords.OrderByDescending(k => k.Change.HasValue ? Math.Abs(k.Change.Value) : Entered(k) || Dropped(k) ? double.MaxValue : -1)
                                   .ThenBy(k => k.Keyword, StringComparer.Ordinal)
                                   .ThenBy(k => k.Country, StringComparer.Ordinal)],
        };
    }

    /// <summary>
    /// Relative change of <paramref name="after"/> against <paramref name="before"/>; null when before is 0 or missing.
    /// </summary>
    /// <param name="before"></param>
    /// <param name="after"></param>
    /// <returns></returns>
    public static double? Relative(double? before, double? after) => before is > 0 && after.HasValue ? (after.Value - before.Value) / before.Value : null;

    /// <summary>
    /// Daily averages of the two windows over the weekdays both have values for. <c>Matched</c> is true when a weekday was
    /// left out (or the counts per weekday differ), so the averages are not the plain ones.
    /// </summary>
    /// <param name="before"></param>
    /// <param name="after"></param>
    /// <returns></returns>
    public static (double? Before, double? After, bool Matched) WeekdayMeans(IReadOnlyList<(DateOnly Date, double Value)> before, IReadOnlyList<(DateOnly Date, double Value)> after)
    {
        if (before.Count == 0 || after.Count == 0)
            return (before.Count > 0 ? before.Average(p => p.Value) : null, after.Count > 0 ? after.Average(p => p.Value) : null, false);

        var beforeByWeekday = before.GroupBy(p => p.Date.DayOfWeek).ToDictionary(g => g.Key, g => g.Select(p => p.Value).ToList());
        var afterByWeekday = after.GroupBy(p => p.Date.DayOfWeek).ToDictionary(g => g.Key, g => g.Select(p => p.Value).ToList());
        var common = beforeByWeekday.Keys.Intersect(afterByWeekday.Keys).ToList();

        // Same weekdays, each as often in both windows: the plain averages are already balanced.
        var balanced = beforeByWeekday.Count == afterByWeekday.Count
                       && common.Count == beforeByWeekday.Count
                       && beforeByWeekday.Values.Select(v => v.Count).Distinct().Count() == 1
                       && afterByWeekday.Values.Select(v => v.Count).Distinct().Count() == 1;

        if (balanced || common.Count == 0)
            return (before.Average(p => p.Value), after.Average(p => p.Value), false);

        return (common.Average(d => beforeByWeekday[d].Average()), common.Average(d => afterByWeekday[d].Average()), true);
    }

    private static (AsoImpactMetricDto Comparison, bool Matched) CompareCount(StoreMetricKind metric, List<(DateOnly Date, StoreMetricValues Values)> before, List<(DateOnly Date, StoreMetricValues Values)> after)
    {
        List<(DateOnly Date, double Value)> Values(List<(DateOnly Date, StoreMetricValues Values)> window)
            => [.. window.Where(d => d.Values?.Value(metric) != null).Select(d => (d.Date, d.Values.Value(metric).Value))];

        var beforeValues = Values(before);
        var afterValues = Values(after);
        var (beforeMean, afterMean, matched) = WeekdayMeans(beforeValues, afterValues);

        return (new AsoImpactMetricDto
        {
            Metric = metric,
            Before = beforeMean,
            After = afterMean,
            BeforeTotal = beforeValues.Count > 0 ? (long)beforeValues.Sum(v => v.Value) : null,
            AfterTotal = afterValues.Count > 0 ? (long)afterValues.Sum(v => v.Value) : null,
            AbsoluteChange = beforeMean.HasValue && afterMean.HasValue ? afterMean - beforeMean : null,
            RelativeChange = Relative(beforeMean, afterMean),
        }, matched);
    }

    private static List<(DateOnly Date, StoreMetricValues Values)> WindowDays(Dictionary<DateOnly, StoreMetricValues> byDay, DateOnly from, int length)
        => [.. Enumerable.Range(0, length).Select(offset => from.AddDays(offset)).Select(day => (day, byDay.TryGetValue(day, out var values) ? values : null))];

    private static long Converting(List<(DateOnly Date, StoreMetricValues Values)> window)
        => window.Where(d => d.Values?.ConvertingDownloads != null && d.Values.PageViews != null).Sum(d => d.Values.ConvertingDownloads.Value);

    private static long Viewed(List<(DateOnly Date, StoreMetricValues Values)> window)
        => window.Where(d => d.Values?.ConvertingDownloads != null && d.Values.PageViews != null).Sum(d => d.Values.PageViews.Value);

    private static AsoImpactDayDto DayDto(int offset, (DateOnly Date, StoreMetricValues Values) day) => new()
    {
        Offset = offset,
        Date = day.Date,
        Impressions = day.Values?.Impressions,
        PageViews = day.Values?.PageViews,
        Downloads = day.Values?.Downloads,
        ConversionRate = day.Values?.ConversionRate,
    };
}

/// <summary>
/// The before and after windows of an effect day.
/// </summary>
/// <param name="BeforeFrom">First day of the before window.</param>
/// <param name="BeforeTo">Last day of the before window (the day before the effect day).</param>
/// <param name="AfterFrom">First day of the after window.</param>
/// <param name="AfterTo">Last day of the after window.</param>
/// <param name="WindowDays">Length of each window.</param>
/// <param name="GapDays">Days skipped from the effect day on.</param>
public sealed record ImpactWindows(DateOnly BeforeFrom, DateOnly BeforeTo, DateOnly AfterFrom, DateOnly AfterTo, int WindowDays, int GapDays)
{
    /// <summary>
    /// Whether each window is whole weeks (every weekday equally often).
    /// </summary>
    public bool WholeWeeks { get; } = WindowDays % 7 == 0;
}

/// <summary>
/// The metrics part of a before and after report.
/// </summary>
public sealed class ImpactMetricsComparison
{
    /// <summary>
    /// Impressions, page views, downloads and conversion rate.
    /// </summary>
    public List<AsoImpactMetricDto> Metrics { get; init; } = [];

    /// <summary>
    /// The conversion rate test.
    /// </summary>
    public AsoImpactConversionTestDto Conversion { get; init; }

    /// <summary>
    /// Days of the before window with data.
    /// </summary>
    public int BeforeDaysWithData { get; init; }

    /// <summary>
    /// Days of the after window with data.
    /// </summary>
    public int AfterDaysWithData { get; init; }

    /// <summary>
    /// Whether a count was averaged over matched weekdays only.
    /// </summary>
    public bool WeekdayMatched { get; init; }

    /// <summary>
    /// Every day of the before window.
    /// </summary>
    public List<AsoImpactDayDto> BeforeDays { get; init; } = [];

    /// <summary>
    /// Every day of the after window.
    /// </summary>
    public List<AsoImpactDayDto> AfterDays { get; init; } = [];
}

/// <summary>
/// The daily rank series of one tracked keyword.
/// </summary>
/// <param name="Keyword">Keyword.</param>
/// <param name="Country">Country.</param>
/// <param name="Points">Days (checked or not).</param>
public sealed record ImpactKeywordSeries(string Keyword, string Country, IReadOnlyList<AsoRankPoint> Points);
