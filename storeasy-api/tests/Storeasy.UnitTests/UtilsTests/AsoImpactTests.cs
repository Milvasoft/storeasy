using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "ASO impact: windows and gap, weekday matching, the conversion z-test, low data, confounding, ranks and the event rules.")]
public class AsoImpactTests
{
    // A Wednesday.
    private static readonly DateOnly _effect = new(2026, 10, 14);

    private static StoreMetricValues Values(long? pageViews, long? downloads, long? impressions = null)
        => new(impressions, pageViews, downloads, null, pageViews != null ? downloads : null);

    private static List<StoreMetricDay> Days(DateOnly from, DateOnly to, Func<DateOnly, StoreMetricValues> values)
    {
        var days = new List<StoreMetricDay>();

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var value = values(day);

            if (value != null)
                days.Add(new StoreMetricDay(day, value));
        }

        return days;
    }

    private static ImpactEvent Event(string id, DateOnly date, int storeAppId = 1, string releaseKey = null, ImpactDateSource source = ImpactDateSource.Applied)
        => new() { Id = id, Date = date, StoreAppId = storeAppId, ReleaseKey = releaseKey, DateSource = source, Kind = ImpactEventKind.Listing, Title = id };

    [Theory]
    [InlineData(14, 2, "2026-09-30", "2026-10-13", "2026-10-16", "2026-10-29", true)]
    [InlineData(7, 0, "2026-10-07", "2026-10-13", "2026-10-14", "2026-10-20", true)]
    [InlineData(28, 2, "2026-09-16", "2026-10-13", "2026-10-16", "2026-11-12", true)]
    [InlineData(10, 3, "2026-10-04", "2026-10-13", "2026-10-17", "2026-10-26", false)]
    public void Windows_ShouldBeEqualAndSkipTheGap(int window, int gap, string beforeFrom, string beforeTo, string afterFrom, string afterTo, bool wholeWeeks)
    {
        var windows = ImpactMath.Windows(_effect, window, gap);

        windows.BeforeFrom.Should().Be(DateOnly.Parse(beforeFrom));
        windows.BeforeTo.Should().Be(DateOnly.Parse(beforeTo));
        windows.AfterFrom.Should().Be(DateOnly.Parse(afterFrom));
        windows.AfterTo.Should().Be(DateOnly.Parse(afterTo));
        (windows.BeforeTo.DayNumber - windows.BeforeFrom.DayNumber).Should().Be(windows.AfterTo.DayNumber - windows.AfterFrom.DayNumber);
        windows.WholeWeeks.Should().Be(wholeWeeks);
    }

    [Fact]
    public void CompareMetrics_ShouldGiveDailyAveragesTotalsAndChanges()
    {
        var windows = ImpactMath.Windows(_effect, 14, 2);

        // 100 views and 10 downloads a day before; 120 and 18 after. The gap days (a spike) must be ignored.
        var days = Days(windows.BeforeFrom, windows.AfterTo, d => d < _effect ? Values(100, 10, 1000)
                                                                  : d < windows.AfterFrom ? Values(5000, 2000, 9000)
                                                                  : Values(120, 18, 1100));

        var result = ImpactMath.CompareMetrics(days, windows);

        var views = result.Metrics.Single(m => m.Metric == StoreMetricKind.PageViews);
        views.Before.Should().Be(100);
        views.After.Should().Be(120);
        views.BeforeTotal.Should().Be(1400);
        views.AfterTotal.Should().Be(1680);
        views.AbsoluteChange.Should().Be(20);
        views.RelativeChange.Should().BeApproximately(0.2, 1e-9);

        var rate = result.Metrics.Single(m => m.Metric == StoreMetricKind.ConversionRate);
        rate.Before.Should().BeApproximately(0.10, 1e-9);
        rate.After.Should().BeApproximately(0.15, 1e-9);
        rate.AbsoluteChange.Should().BeApproximately(0.05, 1e-9);
        rate.RelativeChange.Should().BeApproximately(0.5, 1e-9);

        result.BeforeDaysWithData.Should().Be(14);
        result.AfterDaysWithData.Should().Be(14);
        result.WeekdayMatched.Should().BeFalse("whole weeks with every day reported are balanced already");
        result.BeforeDays.Should().HaveCount(14);
        result.AfterDays.Should().HaveCount(14);
        result.AfterDays[0].Date.Should().Be(windows.AfterFrom);
        result.AfterDays[0].Offset.Should().Be(0);
        result.Conversion.Signal.Should().Be(ImpactSignal.Significant);
    }

    [Fact]
    public void CompareMetrics_ShouldMatchWeekdaysWhenAWindowMissesDays()
    {
        var windows = ImpactMath.Windows(_effect, 7, 0);

        // Weekends have 300 views, weekdays 100, in both windows; the after window misses its Saturday and Sunday. Plain
        // averages would read 157 against 100 (a fake drop); matched weekdays read 100 against 100.
        long ViewsOf(DateOnly d) => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 300 : 100;
        var days = Days(windows.BeforeFrom, windows.AfterTo, d => d >= windows.AfterFrom && d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : Values(ViewsOf(d), 10));

        var result = ImpactMath.CompareMetrics(days, windows);
        var views = result.Metrics.Single(m => m.Metric == StoreMetricKind.PageViews);

        result.AfterDaysWithData.Should().Be(5);
        result.WeekdayMatched.Should().BeTrue();
        views.Before.Should().Be(100);
        views.After.Should().Be(100);
        views.RelativeChange.Should().Be(0);
    }

    [Fact]
    public void WeekdayMeans_ShouldBalanceAWindowThatIsNotWholeWeeks()
    {
        // Ten days from a Monday hold two Mondays, Tuesdays and Wednesdays; the other weekdays once.
        var monday = new DateOnly(2026, 10, 5);
        var before = Enumerable.Range(0, 10).Select(i => (monday.AddDays(i), monday.AddDays(i).DayOfWeek == DayOfWeek.Monday ? 50d : 10d)).ToList();
        var after = Enumerable.Range(14, 10).Select(i => (monday.AddDays(i), monday.AddDays(i).DayOfWeek == DayOfWeek.Monday ? 50d : 10d)).ToList();

        var (b, a, matched) = ImpactMath.WeekdayMeans(before, after);

        a.Should().Be(b, "the same weekday pattern reads the same after balancing");
        matched.Should().BeTrue();
        b.Should().BeApproximately((50d + (6 * 10)) / 7, 1e-9);
    }

    [Fact]
    public void ConversionTest_ShouldMatchAKnownZScore()
    {
        // 100 of 1000 against 130 of 1000: pooled 0.115, se 0.014267, z 2.1028, p 0.0355.
        var test = ImpactMath.ConversionTest(100, 1000, 130, 1000);

        test.ZScore.Should().BeApproximately(2.1028, 0.001);
        test.PValue.Should().BeApproximately(0.0355, 0.001);
        test.Signal.Should().Be(ImpactSignal.Significant);
    }

    [Theory]
    [InlineData(100, 1000, 112, 1000, ImpactSignal.NotSignificant)]
    [InlineData(100, 1000, 125, 1000, ImpactSignal.Suggestive)]
    [InlineData(100, 1000, 60, 1000, ImpactSignal.Significant)]
    [InlineData(5, 99, 30, 1000, ImpactSignal.InsufficientData)]
    [InlineData(5, 500, 6, 500, ImpactSignal.InsufficientData)]
    [InlineData(150, 120, 160, 130, ImpactSignal.NotApplicable)]
    public void ConversionTest_ShouldGiveTheSignal(long beforeDownloads, long beforeViews, long afterDownloads, long afterViews, ImpactSignal expected)
    {
        var test = ImpactMath.ConversionTest(beforeDownloads, beforeViews, afterDownloads, afterViews);

        test.Signal.Should().Be(expected);

        if (expected is ImpactSignal.InsufficientData or ImpactSignal.NotApplicable)
        {
            test.ZScore.Should().BeNull();
            test.PValue.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(0, 0.5)]
    [InlineData(1.96, 0.975)]
    [InlineData(-1.96, 0.025)]
    [InlineData(1.6449, 0.95)]
    public void NormalCdf_ShouldMatchTheTable(double z, double expected) => ImpactMath.NormalCdf(z).Should().BeApproximately(expected, 1e-4);

    [Fact]
    public void CompareMetrics_ShouldSayNotEnoughDataForASmallApp()
    {
        var windows = ImpactMath.Windows(_effect, 14, 2);
        var days = Days(windows.BeforeFrom, windows.AfterTo, d => Values(3, 1));

        var result = ImpactMath.CompareMetrics(days, windows);

        result.Conversion.Signal.Should().Be(ImpactSignal.InsufficientData);
        result.Conversion.BeforePageViews.Should().Be(42);
    }

    [Fact]
    public void CompareMetrics_ShouldLeaveUnreportedMetricsNull()
    {
        var windows = ImpactMath.Windows(_effect, 7, 2);

        // Google Play reports no impressions.
        var result = ImpactMath.CompareMetrics(Days(windows.BeforeFrom, windows.AfterTo, d => Values(100, 10)), windows);
        var impressions = result.Metrics.Single(m => m.Metric == StoreMetricKind.Impressions);

        impressions.Before.Should().BeNull();
        impressions.After.Should().BeNull();
        impressions.RelativeChange.Should().BeNull();
        impressions.BeforeTotal.Should().BeNull();
    }

    [Fact]
    public void Overlapping_ShouldFlagOtherChangesOfTheRecordInsideTheWindows()
    {
        var windows = ImpactMath.Windows(_effect, 14, 2);
        var target = Event("plan-1", _effect, releaseKey: "1:v2");
        var others = new[]
        {
            target,
            Event("plan-2", _effect.AddDays(5)),                                        // inside the after window
            Event("plan-3", _effect.AddDays(1)),                                        // in the gap
            Event("plan-4", _effect.AddDays(-20)),                                      // before the before window
            Event("plan-5", _effect.AddDays(-3), storeAppId: 2),                        // another store record
            Event("version-6", _effect, releaseKey: "1:v2"),                            // released together
            Event("plan-7", _effect.AddDays(3), source: ImpactDateSource.AwaitingRelease), // not live
            Event("plan-8", windows.AfterTo.AddDays(1)),                                // after the after window
        };

        var (confounders, related) = ImpactMath.Overlapping(target, others, windows);

        confounders.Select(e => e.Id).Should().Equal("plan-3", "plan-2");
        related.Select(e => e.Id).Should().Equal("version-6");
    }

    [Fact]
    public void Status_ShouldFollowTheWindowsAndTheRelease()
    {
        var windows = ImpactMath.Windows(_effect, 14, 2);
        var withData = ImpactMath.CompareMetrics(Days(windows.BeforeFrom, windows.BeforeTo, d => Values(100, 10)), windows);
        var empty = ImpactMath.CompareMetrics([], windows);

        ImpactMath.Status(ImpactDateSource.Applied, windows, withData, windows.AfterTo.AddDays(1)).Should().Be(ImpactStatus.Ready);
        ImpactMath.Status(ImpactDateSource.Applied, windows, withData, windows.AfterTo).Should().Be(ImpactStatus.Collecting);
        ImpactMath.Status(ImpactDateSource.Applied, windows, empty, windows.AfterTo.AddDays(5)).Should().Be(ImpactStatus.NoData);
        ImpactMath.Status(ImpactDateSource.AwaitingRelease, windows, withData, windows.AfterTo.AddDays(5)).Should().Be(ImpactStatus.AwaitingRelease);
    }

    [Fact]
    public void CompareRanks_ShouldAverageRankedDaysAndCountMoves()
    {
        var windows = ImpactMath.Windows(_effect, 7, 2);

        List<AsoRankPoint> Series(Func<DateOnly, (bool Checked, int? Rank)> rank)
        {
            var points = new List<AsoRankPoint>();

            for (var day = windows.BeforeFrom; day <= windows.AfterTo; day = day.AddDays(1))
            {
                var (isChecked, value) = rank(day);
                points.Add(new AsoRankPoint(day, isChecked, value));
            }

            return points;
        }

        var series = new[]
        {
            // 12 before, 6 after: up 6, enters the top 10.
            new ImpactKeywordSeries("pusula", "tr", Series(d => (true, d < _effect ? 12 : 6))),
            // 4 before, 9 after: down 5, still top 10.
            new ImpactKeywordSeries("kible", "tr", Series(d => (true, d < _effect ? 4 : 9))),
            // Not in the results before, ranked 30 after: newly ranked.
            new ImpactKeywordSeries("namaz", "tr", Series(d => (true, d < _effect ? null : 30))),
            // Ranked before, not checked after (tracking stopped): neither improved nor declined.
            new ImpactKeywordSeries("harita", "tr", Series(d => (d < _effect, d < _effect ? 20 : null))),
            // Never checked in the windows: left out.
            new ImpactKeywordSeries("yon", "tr", Series(d => (false, null))),
        };

        var ranks = ImpactMath.CompareRanks(series, windows);

        ranks.KeywordCount.Should().Be(4);
        ranks.Compared.Should().Be(2);
        ranks.BeforeAverage.Should().Be(8);
        ranks.AfterAverage.Should().Be(7.5);
        ranks.BeforeTop10.Should().Be(1);
        ranks.AfterTop10.Should().Be(2);
        ranks.Improved.Should().Be(2);
        ranks.Declined.Should().Be(1);
        ranks.Keywords.Select(k => k.Keyword).Should().StartWith("namaz");
        var pusula = ranks.Keywords.Single(k => k.Keyword == "pusula");
        pusula.Change.Should().Be(-6);
        pusula.BeforeCheckedDays.Should().Be(7);
        pusula.AfterRankedDays.Should().Be(7);
    }

    [Theory]
    [InlineData(ChangePlanKind.Listing, null, null, ImpactEventKind.Listing)]
    [InlineData(ChangePlanKind.Categories, null, null, ImpactEventKind.Category)]
    [InlineData(ChangePlanKind.Pricing, null, null, ImpactEventKind.Price)]
    [InlineData(ChangePlanKind.Previews, null, null, ImpactEventKind.Previews)]
    [InlineData(ChangePlanKind.Screenshots, "Media", "phoneScreenshots", ImpactEventKind.Screenshots)]
    [InlineData(ChangePlanKind.Screenshots, "Media", "icon", ImpactEventKind.Icon)]
    [InlineData(ChangePlanKind.TrackRelease, "Track", "production", ImpactEventKind.VersionReleased)]
    [InlineData(ChangePlanKind.TrackRelease, "Track", "internal", null)]
    [InlineData(ChangePlanKind.BundleUpload, "Track", "production", ImpactEventKind.VersionReleased)]
    [InlineData(ChangePlanKind.ReviewInfo, "Review", "notes", null)]
    [InlineData(ChangePlanKind.MsChange, "listing", "description", ImpactEventKind.Listing)]
    [InlineData(ChangePlanKind.MsChange, "image", "Screenshot", ImpactEventKind.Screenshots)]
    [InlineData(ChangePlanKind.MsChange, "pricing", "priceId", ImpactEventKind.Price)]
    [InlineData(ChangePlanKind.MsChange, "package", "package", null)]
    public void KindOf_ShouldMapStorePageChangesOnly(ChangePlanKind kind, string group, string field, ImpactEventKind? expected)
    {
        var items = group == null ? new List<PlanItem>() : [new PlanItem { Group = group, Field = field }];

        ImpactEventRules.KindOf(kind, items).Should().Be(expected);
    }

    [Fact]
    public void KindOf_ShouldCallAMixedMicrosoftChangeAMicrosoftChange()
        => ImpactEventRules.KindOf(ChangePlanKind.MsChange, [new PlanItem { Group = "listing", Field = "title" }, new PlanItem { Group = "pricing", Field = "priceId" }]).Should().Be(ImpactEventKind.MsChange);

    [Fact]
    public void AppleEffect_ShouldPreferTheLiveDay()
    {
        var applied = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
        var live = new DateTime(2026, 10, 4, 18, 0, 0, DateTimeKind.Utc);

        ImpactEventRules.AppleEffect(applied, "READY_FOR_SALE", live).Should().Be((live, ImpactDateSource.StoreLive));
        ImpactEventRules.AppleEffect(applied, "READY_FOR_SALE", applied.AddDays(-30)).Should().Be((applied, ImpactDateSource.Applied), "an edit of the live version is live when applied");
        ImpactEventRules.AppleEffect(applied, "WAITING_FOR_REVIEW", applied).Should().Be((applied, ImpactDateSource.AwaitingRelease));
        ImpactEventRules.AppleEffect(applied, "PENDING_DEVELOPER_RELEASE", applied).Should().Be((applied, ImpactDateSource.AwaitingRelease));
        ImpactEventRules.AppleEffect(applied, "REPLACED_WITH_NEW_VERSION", live).Should().Be((applied, ImpactDateSource.Applied), "the live day of a replaced version is not known");
        ImpactEventRules.AppleEffect(applied, null, null).Should().Be((applied, ImpactDateSource.Applied));
    }

    [Fact]
    public void LocalesAndFields_ShouldBeDistinctAndSorted()
    {
        List<PlanItem> items =
        [
            new() { Locale = "tr", Field = "subtitle" },
            new() { Locale = "en-US", Field = "description" },
            new() { Locale = "tr", Field = "description" },
            new() { Locale = null, Field = "phoneScreenshots.order" },
        ];

        ImpactEventRules.Locales(items).Should().Equal("en-US", "tr");
        ImpactEventRules.Fields(items).Should().Equal("description", "phoneScreenshots", "subtitle");
    }

    [Fact]
    public void Merge_ShouldKeepOnePerIdNewestFirst()
    {
        var merged = ImpactEventRules.Merge([[Event("plan-1", _effect), Event("plan-2", _effect.AddDays(3))], [Event("plan-1", _effect.AddDays(-9)), Event("version-3", _effect.AddDays(1))]]);

        merged.Select(e => e.Id).Should().Equal("plan-2", "version-3", "plan-1");
        merged.Single(e => e.Id == "plan-1").Date.Should().Be(_effect, "the first source wins");
    }

    [Fact]
    public void MsPublishedDate_ShouldBeTheFirstPublishedStatus()
    {
        var published = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);

        ImpactEventRules.MsPublishedDate([new MsStatusChange { Status = MsSubmissionStatus.Certification, Date = published.AddDays(-2) }, new MsStatusChange { Status = MsSubmissionStatus.Published, Date = published }, new MsStatusChange { Status = MsSubmissionStatus.Published, Date = published.AddDays(4) }])
                        .Should().Be(published);
        ImpactEventRules.MsPublishedDate([new MsStatusChange { Status = MsSubmissionStatus.Certification, Date = published }]).Should().BeNull();
        ImpactEventRules.MsPublishedDate(null).Should().BeNull();
    }
}
