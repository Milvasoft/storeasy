using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Enums;
using System.Text.Json;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Store page metrics: report parsers of the three stores, slices, merging, conversion math, ranges and the Play bucket.")]
public class StoreMetricReportsTests
{
    private static readonly DateOnly _nov18 = new(2026, 11, 18);
    private static readonly DateOnly _nov19 = new(2026, 11, 19);
    private static readonly DateOnly _oct1 = new(2026, 10, 1);

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "UtilsTests", "Fixtures", "StoreMetrics", name);

    private static string Text(string name) => File.ReadAllText(FixturePath(name));

    private static StoreMetricRow Row(IEnumerable<StoreMetricRow> rows, DateOnly date, string country = "", TrafficSource source = TrafficSource.All)
        => rows.Single(r => r.Date == date && r.Country == country && r.Source == source);

    [Fact]
    public void AppleEngagement_ShouldCountImpressionsAndStorePageViewsPerSlice()
    {
        // Act
        var rows = AppleAnalyticsReports.ParseEngagement(Text("apple_engagement_standard.tsv"));

        // Assert
        Row(rows, _nov18).Should().Be(new StoreMetricRow(_nov18, "", TrafficSource.All, 2000, 340, null, null), "taps and version history views are not page views");
        Row(rows, _nov18, "TR").Should().Match<StoreMetricRow>(r => r.Impressions == 1500 && r.PageViews == 240);
        Row(rows, _nov18, source: TrafficSource.Search).Should().Match<StoreMetricRow>(r => r.Impressions == 1700 && r.PageViews == 240);
        Row(rows, _nov18, "TR", TrafficSource.Search).Should().Match<StoreMetricRow>(r => r.Impressions == 1200 && r.PageViews == 240);
        Row(rows, _nov18, source: TrafficSource.AppReferrer).PageViews.Should().Be(40, "a store sheet is the product page shown in another app");
        Row(rows, _nov18, "DE").Impressions.Should().BeNull();
        Row(rows, _nov19).Should().Match<StoreMetricRow>(r => r.Impressions == 1000 && r.PageViews == 200);
    }

    [Fact]
    public void AppleDownloads_ShouldCountFirstTimeDownloadsAndRedownloadsButNotUpdates()
    {
        // Act
        var rows = AppleAnalyticsReports.ParseDownloads(Text("apple_downloads_standard.tsv"));

        // Assert
        Row(rows, _nov18).Should().Be(new StoreMetricRow(_nov18, "", TrafficSource.All, null, null, 46, 5));
        Row(rows, _nov18, "TR").Downloads.Should().Be(40);
        Row(rows, _nov18, source: TrafficSource.Browse).Downloads.Should().Be(10);
        Row(rows, _nov18, source: TrafficSource.WebReferrer).Should().Match<StoreMetricRow>(r => r.Downloads == 6 && r.Redownloads == null);
        rows.Should().NotContain(r => r.Source == TrafficSource.Unavailable, "the only Unavailable row is an auto-update");
    }

    [Fact]
    public void AppleReports_ShouldMergeIntoOneRowPerSlice()
    {
        // Act
        var rows = StoreMetricRows.Merge([.. AppleAnalyticsReports.ParseEngagement(Text("apple_engagement_standard.tsv")),
                                          .. AppleAnalyticsReports.ParseDownloads(Text("apple_downloads_standard.tsv"))]);

        // Assert
        Row(rows, _nov18).Should().Be(new StoreMetricRow(_nov18, "", TrafficSource.All, 2000, 340, 46, 5));
        Row(rows, _nov18, "TR", TrafficSource.Search).Should().Be(new StoreMetricRow(_nov18, "TR", TrafficSource.Search, 1200, 240, 30, 5));
        rows.Select(r => (r.Date, r.Country, r.Source)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void LatestPerDay_ShouldLetALaterInstanceReplaceADayInsteadOfAddingToIt()
    {
        // Arrange
        var first = new List<StoreMetricRow> { new(_nov18, "", TrafficSource.All, 100, 10, 1, null), new(_nov19, "", TrafficSource.All, 50, 5, null, null) };
        var correction = new List<StoreMetricRow> { new(_nov19, "", TrafficSource.All, 60, 6, 2, null) };

        // Act
        var rows = StoreMetricRows.LatestPerDay([first, correction]);

        // Assert
        rows.Should().Equal(new StoreMetricRow(_nov18, "", TrafficSource.All, 100, 10, 1, null), new StoreMetricRow(_nov19, "", TrafficSource.All, 60, 6, 2, null));
    }

    [Fact]
    public void PlayStorePerformance_ShouldReadUtf16FilesAndNotCountTheSourceFileInTheTotals()
    {
        // Arrange
        var accumulator = new StoreMetricAccumulator();

        // Act
        PlayStatsReports.AddCountryFile(DelimitedText.Decode(File.ReadAllBytes(FixturePath("store_performance_com.example.pusula_202610_country.csv"))), accumulator);
        PlayStatsReports.AddTrafficSourceFile(DelimitedText.Decode(File.ReadAllBytes(FixturePath("store_performance_com.example.pusula_202610_traffic_source.csv"))), accumulator);
        var rows = accumulator.ToRows();

        // Assert
        Row(rows, _oct1).Should().Be(new StoreMetricRow(_oct1, "", TrafficSource.All, null, 140, 15, null));
        Row(rows, _oct1, "TR").Should().Match<StoreMetricRow>(r => r.PageViews == 100 && r.Downloads == 12);
        Row(rows, _oct1, source: TrafficSource.Search).Should().Match<StoreMetricRow>(r => r.PageViews == 80 && r.Downloads == 10, "search terms are summed per source");
        Row(rows, _oct1, source: TrafficSource.Browse).PageViews.Should().Be(40);
        Row(rows, _oct1, source: TrafficSource.Referral).Downloads.Should().Be(1);
        Row(rows, _oct1.AddDays(1), "TR").Should().Match<StoreMetricRow>(r => r.PageViews == 10030 && r.Downloads == 1204, "quoted counts carry grouping commas");
        rows.Should().NotContain(r => r.Country != "" && r.Source != TrafficSource.All, "Play has no country and source cross");
    }

    [Fact]
    public void PlayStatsReports_ShouldNameTheMonthlyFilesAndMonths()
    {
        // Act
        var months = PlayStatsReports.Months(new DateOnly(2026, 6, 15), new DateOnly(2026, 11, 21));

        // Assert
        months.Should().Equal(new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1));
        PlayStatsReports.CountryFile("com.example.pusula", _oct1).Should().Be("stats/store_performance/store_performance_com.example.pusula_202610_country.csv");
        PlayStatsReports.TrafficSourceFile("com.example.pusula", _oct1).Should().Be("stats/store_performance/store_performance_com.example.pusula_202610_traffic_source.csv");
    }

    [Fact]
    public void MicrosoftAnalytics_ShouldSliceAcquisitionsAndPutFunnelPageViewsOnTheTotal()
    {
        // Arrange
        var accumulator = new StoreMetricAccumulator();
        using var acquisitions = JsonDocument.Parse(Text("ms_appacquisitions.json"));
        using var funnel = JsonDocument.Parse(Text("ms_funnel.json"));

        // Act
        MicrosoftAnalyticsReports.AddAcquisitions(acquisitions.RootElement, accumulator);
        MicrosoftAnalyticsReports.AddFunnelDay(funnel.RootElement, _nov18, accumulator);
        var rows = accumulator.ToRows();

        // Assert
        Row(rows, _nov18).Should().Be(new StoreMetricRow(_nov18, "", TrafficSource.All, null, 40, 10, null), "UserCount comes as a string or a number");
        Row(rows, _nov18, "TR", TrafficSource.StoreClient).Downloads.Should().Be(7);
        Row(rows, _nov18, source: TrafficSource.StoreWeb).Downloads.Should().Be(3);
        Row(rows, _nov18, "TR").PageViews.Should().BeNull("the funnel has no country");
        Row(rows, _nov19).Downloads.Should().Be(4);
        MicrosoftAnalyticsReports.NextLink(acquisitions.RootElement).Should().Contain("skip=10000");
    }

    [Theory]
    [InlineData("App Store search", TrafficSource.Search)]
    [InlineData("App Store browse", TrafficSource.Browse)]
    [InlineData("App referrer", TrafficSource.AppReferrer)]
    [InlineData("Web referrer", TrafficSource.WebReferrer)]
    [InlineData("Institutional purchase", TrafficSource.Other)]
    [InlineData("", TrafficSource.Unavailable)]
    public void AppleSource_ShouldMapTheSourceTypes(string value, TrafficSource expected) => AppleAnalyticsReports.Source(value).Should().Be(expected);

    [Fact]
    public void Accumulator_ShouldAddAFactToEveryCoarserSlice()
    {
        // Arrange
        var accumulator = new StoreMetricAccumulator();

        // Act
        accumulator.Add(_nov18, " tr ", TrafficSource.Search, StoreMetricKind.Downloads, 3);
        accumulator.Add(_nov18, "", TrafficSource.Search, StoreMetricKind.Downloads, 1);
        accumulator.Touch(_nov18, null, null, StoreMetricKind.PageViews);
        var rows = accumulator.ToRows();

        // Assert
        rows.Select(r => (r.Country, r.Source, r.Downloads)).Should().BeEquivalentTo([("", TrafficSource.All, 4L), ("", TrafficSource.Search, 4L), ("TR", TrafficSource.All, 3L), ("TR", TrafficSource.Search, 3L), (StoreMetricRules.UnknownCountry, TrafficSource.All, 1L), (StoreMetricRules.UnknownCountry, TrafficSource.Search, 1L)]);
        Row(rows, _nov18).PageViews.Should().Be(0, "a touched metric is reported as zero, not missing");
        FluentActions.Invoking(() => accumulator.Add(_nov18, null, null, StoreMetricKind.ConversionRate, 1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConversionMath_ShouldDivideOnlyDownloadsThatHavePageViews()
    {
        // Arrange: App Store 10 of 100 converted, Play reports 40 downloads with 200 visitors, Microsoft 5 downloads without page views.
        var values = new StoreMetricValues(1000, 300, 55, null, 50);

        // Assert
        values.ConversionRate.Should().BeApproximately(50d / 300, 0.000001);
        values.Value(StoreMetricKind.Downloads).Should().Be(55);
        StoreMetricRules.ConversionRate(5, 0).Should().BeNull();
        StoreMetricRules.ConversionRate(null, 10).Should().BeNull();
        StoreMetricRules.Change(150, 100).Should().Be(0.5);
        StoreMetricRules.Change(50, 100).Should().Be(-0.5);
        StoreMetricRules.Change(10, 0).Should().BeNull();
        StoreMetricRules.Change(null, 10).Should().BeNull();
        StoreMetricValues.Empty.ConversionRate.Should().BeNull();
    }

    [Fact]
    public void Range_ShouldDefaultToThirtyDaysEndingYesterdayAndRefuseBadRanges()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);

        // Act
        var defaults = StoreMetricRange.Resolve(null, null, null, now);
        var week = StoreMetricRange.Resolve(null, null, 7, now);

        // Assert
        defaults.Should().Be(new StoreMetricRange(new DateOnly(2026, 10, 22), new DateOnly(2026, 11, 20)));
        defaults.Days.Should().Be(30);
        defaults.PreviousFrom.Should().Be(new DateOnly(2026, 9, 22));
        defaults.PreviousTo.Should().Be(new DateOnly(2026, 10, 21));
        week.Days.Should().Be(7);
        StoreMetricRange.Resolve(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 1), null, now).Should().BeNull();
        StoreMetricRange.Resolve(new DateOnly(2024, 1, 1), new DateOnly(2026, 1, 1), null, now).Should().BeNull();
    }

    [Theory]
    [InlineData("gs://pubsite_prod_rev_01234567890987654321/", "pubsite_prod_rev_01234567890987654321")]
    [InlineData("  pubsite_prod_rev_123/stats/installs ", "pubsite_prod_rev_123")]
    [InlineData("GS://PUBSITE_PROD_123", "pubsite_prod_123")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("not a bucket!", null)]
    [InlineData("gs://", null)]
    public void ReportBuckets_ShouldKeepTheBucketName(string value, string expected) => ReportBuckets.Normalize(value).Should().Be(expected);

    [Fact]
    public void DelimitedText_ShouldHandleQuotesTabsAndMissingCells()
    {
        // Act
        var csv = DelimitedText.Parse("Date,\"Note, with comma\",Counts\r\n2026-11-18,\"say \"\"hi\"\"\",7\r\n\r\n2026-11-19,x\r\n");
        var tsv = DelimitedText.Parse("Date\tCounts\n2026-11-18\t1,234\n");

        // Assert
        csv.Rows.Should().HaveCount(2);
        DelimitedText.Cell(csv.Rows[0], csv.Column("note with comma")).Should().Be("say \"hi\"");
        DelimitedText.Count(csv.Rows[1], csv.Column("Counts")).Should().BeNull("the row is shorter than the header");
        DelimitedText.Count(tsv.Rows[0], tsv.Column("counts")).Should().Be(1234);
        DelimitedText.Date(tsv.Rows[0], 0).Should().Be(_nov18);
        tsv.Column("Missing").Should().Be(-1);
    }

    [Fact]
    public void AppleParser_ShouldFailLoudlyWhenARequiredColumnIsMissing()
        => FluentActions.Invoking(() => AppleAnalyticsReports.ParseDownloads("Date\tTerritory\n2026-11-18\tTR\n")).Should().Throw<FormatException>().WithMessage("*Download Type*");
}
