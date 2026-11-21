using FluentAssertions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Aso;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Events;
using Storeasy.Domain.Experiments;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.MsStore;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "ASO impact: events from plans, versions and Microsoft submissions with the live day, the before and after report with ranks and confounding, the organization list, isolation and validation.")]
public class AsoImpactTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);

    // The effect day: old enough for a final 14 day report with the default gap.
    private static readonly DateOnly _effect = _today.AddDays(-40);

    private static DateTime At(DateOnly day, int hour = 10) => day.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

    private sealed record Seeded(EventOrg Org, int AppId, int AppleRecordId, int PlayRecordId, int MsRecordId, long LivePlanId, long AwaitingPlanId, long PlayListingPlanId, long PlayIconPlanId, long MsPlanId, long VersionStateId, long MsSubmissionId, long DiscardedPlanId, long ReviewPlanId, int ExperimentId, long WinnerPlanId);

    private static ChangePlan Plan(EventOrg org, int appId, int storeAppId, StoreKind store, ChangePlanKind kind, DateTime applied, List<PlanItem> items, string versionId = null, ChangePlanStatus status = ChangePlanStatus.Applied) => new()
    {
        OrganizationId = org.OrganizationId,
        AppId = appId,
        StoreAppId = storeAppId,
        Store = store,
        Kind = kind,
        Status = status,
        Title = $"{kind} change",
        Source = ActionChannel.Web,
        RequiredPermission = OrgPermission.EditContent,
        VersionId = versionId,
        VersionString = versionId == null ? null : "2.0",
        Items = items,
        Warnings = [],
        Calls = [],
        Files = [],
        BaseKeys = [],
        Changes = "{}",
        CreationDate = applied.AddHours(-1),
        ExpiryDate = applied.AddDays(7),
        DecisionDate = status == ChangePlanStatus.Applied ? applied.AddMinutes(-5) : null,
        AppliedDate = status == ChangePlanStatus.Applied ? applied : null,
    };

    private static StoreMetric Metric(EventOrg org, int storeAppId, StoreKind store, DateOnly day, string country, long? impressions, long pageViews, long downloads) => new()
    {
        OrganizationId = org.OrganizationId,
        StoreAppId = storeAppId,
        Store = store,
        Date = day,
        Country = country,
        Source = TrafficSource.All,
        Impressions = impressions,
        PageViews = pageViews,
        Downloads = downloads,
        UpdatedDate = DateTime.UtcNow,
    };

    private async Task<Seeded> SeedAsync()
    {
        var org = await _factory.NewOrgAsync();
        var apple = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Apple, ExternalId = $"6{Random.Shared.Next(100000, 999999)}{org.Suffix}", Name = "Pusula", StoreConnectionId = org.AppleConnectionId, SyncedDate = DateTime.UtcNow };
        var play = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Google, ExternalId = $"com.{org.Suffix}.pusula", Name = "Pusula", StoreConnectionId = org.GoogleConnectionId, SyncedDate = DateTime.UtcNow };
        var microsoft = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Microsoft, ExternalId = $"9N{org.Suffix}".ToUpperInvariant(), Name = "Pusula", SyncedDate = DateTime.UtcNow };
        var app = new App { OrganizationId = org.OrganizationId, Name = "Pusula", StoreApps = [apple, play, microsoft], CreationDate = DateTime.UtcNow };

        await _factory.DbAsync(async db =>
        {
            db.Apps.Add(app);
            await db.SaveChangesAsync();
        });

        var liveVersion = $"v-live-{org.Suffix}";
        var pendingVersion = $"v-review-{org.Suffix}";

        // App Store: a listing written to 2.0 three days before 2.0 went live; another written to a version still in review.
        var livePlan = Plan(org, app.Id, apple.Id, StoreKind.Apple, ChangePlanKind.Listing, At(_effect.AddDays(-3)), [new PlanItem { Group = ContentGroups.Version, Locale = "tr", Field = "subtitle", Before = "Eski", After = "Yeni", Action = ChangeAction.Update }], liveVersion);
        var awaitingPlan = Plan(org, app.Id, apple.Id, StoreKind.Apple, ChangePlanKind.Listing, At(_today.AddDays(-2)), [new PlanItem { Group = ContentGroups.Version, Locale = "en-US", Field = "description", After = "New", Action = ChangeAction.Update }], pendingVersion);
        var discardedPlan = Plan(org, app.Id, apple.Id, StoreKind.Apple, ChangePlanKind.Listing, At(_effect), [], status: ChangePlanStatus.Discarded);
        var reviewPlan = Plan(org, app.Id, apple.Id, StoreKind.Apple, ChangePlanKind.ReviewInfo, At(_effect), [new PlanItem { Group = ContentGroups.Review, Field = "notes", After = "x", Action = ChangeAction.Update }]);

        // Google Play: a listing change, and an icon change six days later (inside its after window).
        var playListing = Plan(org, app.Id, play.Id, StoreKind.Google, ChangePlanKind.Listing, At(_effect), [new PlanItem { Group = ContentGroups.Listing, Locale = "tr-TR", Field = "shortDescription", After = "Kısa", Action = ChangeAction.Update }]);
        var playIcon = Plan(org, app.Id, play.Id, StoreKind.Google, ChangePlanKind.Screenshots, At(_effect.AddDays(6)), [new PlanItem { Group = ContentGroups.Media, Locale = "tr-TR", Field = "icon", After = "icon.png", Action = ChangeAction.Create }]);

        // Microsoft Store: a listing change applied two days before the submission was published.
        var msPlan = Plan(org, app.Id, microsoft.Id, StoreKind.Microsoft, ChangePlanKind.MsChange, At(_effect.AddDays(-22)), [new PlanItem { Group = "listing", Locale = "tr-tr", Field = "description", After = "Yeni", Action = ChangeAction.Update }]);

        // A product page optimization test that ran and ended before the windows, its winner applied the day after; a custom
        // product page (its own traffic) is no event.
        var experiment = new AsoExperiment { OrganizationId = org.OrganizationId, AppId = app.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, Kind = AsoExperimentKind.ProductPageOptimization, Name = "Yeni ikon testi", Status = AsoExperimentStatus.Completed, StartDate = At(_effect.AddDays(-35)), EndDate = At(_effect.AddDays(-26)), CreationDate = At(_effect.AddDays(-36)) };
        var customPage = new AsoExperiment { OrganizationId = org.OrganizationId, AppId = app.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, Kind = AsoExperimentKind.CustomProductPage, Name = "Kampanya sayfası", Status = AsoExperimentStatus.Running, StartDate = At(_effect.AddDays(-30)), CreationDate = At(_effect.AddDays(-31)) };
        var winnerPlan = Plan(org, app.Id, apple.Id, StoreKind.Apple, ChangePlanKind.AsoExperiment, At(_effect.AddDays(-25)), [new PlanItem { Group = ContentGroups.Media, Locale = "tr", Field = "APP_IPHONE_67", After = "treatment-b.png", Action = ChangeAction.Create }]);

        var versionState = new AppVersionState { OrganizationId = org.OrganizationId, AppId = app.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, ExternalVersionId = liveVersion, VersionString = "2.0", State = "READY_FOR_SALE", PreviousState = "PENDING_DEVELOPER_RELEASE", ChangedDate = At(_effect, 16), SubmittedDate = At(_effect.AddDays(-2)) };
        var pendingState = new AppVersionState { OrganizationId = org.OrganizationId, AppId = app.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, ExternalVersionId = pendingVersion, VersionString = "2.1", State = "IN_REVIEW", ChangedDate = At(_today.AddDays(-1)) };
        var submission = new MsSubmission
        {
            OrganizationId = org.OrganizationId,
            StoreAppId = microsoft.Id,
            Target = MsSubmissionTarget.App,
            ExternalId = $"14{org.Suffix}",
            PackageVersion = "1.2.0.0",
            Status = MsSubmissionStatus.Published,
            CreationDate = At(_effect.AddDays(-23)),
            StatusHistory = [new MsStatusChange { Status = MsSubmissionStatus.Certification, Date = At(_effect.AddDays(-21)) }, new MsStatusChange { Status = MsSubmissionStatus.Published, Date = At(_effect.AddDays(-20)) }],
        };

        await _factory.DbAsync(async db =>
        {
            db.ChangePlans.AddRange(livePlan, awaitingPlan, discardedPlan, reviewPlan, playListing, playIcon, msPlan, winnerPlan);
            db.AsoExperiments.AddRange(experiment, customPage);
            db.AppVersionStates.AddRange(versionState, pendingState);
            db.MsSubmissions.Add(submission);

            // App Store: 1000 page views a day; 100 downloads before, 150 after; a spike in the gap that must not count.
            // Every view comes from Turkey, so the TR slice equals the total.
            for (var day = _effect.AddDays(-20); day <= _effect.AddDays(20); day = day.AddDays(1))
            {
                var downloads = day < _effect ? 100 : day < _effect.AddDays(2) ? 900 : 150;

                db.StoreMetrics.Add(Metric(org, apple.Id, StoreKind.Apple, day, StoreMetricRules.AllCountries, 10000, 1000, downloads));
                db.StoreMetrics.Add(Metric(org, apple.Id, StoreKind.Apple, day, "TR", 10000, 1000, downloads));
                db.StoreMetrics.Add(Metric(org, play.Id, StoreKind.Google, day, StoreMetricRules.AllCountries, null, 400, 40));
            }

            var keyword = new AsoTrackedKeyword { OrganizationId = org.OrganizationId, AppId = app.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, Keyword = "pusula", Country = "tr", IsActive = true, TrackedDate = At(_effect.AddDays(-30)) };
            db.AsoTrackedKeywords.Add(keyword);
            await db.SaveChangesAsync();

            for (var day = _effect.AddDays(-14); day <= _effect.AddDays(15); day = day.AddDays(1))
                db.AsoKeywordRanks.Add(new AsoKeywordRank { OrganizationId = org.OrganizationId, TrackedKeywordId = keyword.Id, StoreAppId = apple.Id, Store = StoreKind.Apple, Keyword = "pusula", Country = "tr", Date = day, Rank = day < _effect ? 15 : 8, Depth = 200, CheckedDate = At(day) });

            await db.SaveChangesAsync();
        });

        return new Seeded(org, app.Id, apple.Id, play.Id, microsoft.Id, livePlan.Id, awaitingPlan.Id, playListing.Id, playIcon.Id, msPlan.Id, versionState.Id, submission.Id, discardedPlan.Id, reviewPlan.Id, experiment.Id, winnerPlan.Id);
    }

    private static string Url(Seeded s, string path, string query = "") => $"aso-impact{path}?organizationId={s.Org.OrganizationId}&appId={s.AppId}{query}";

    [Fact]
    public async Task Events_ShouldListStorePageChangesWithTheirLiveDay()
    {
        // Arrange
        var s = await SeedAsync();

        // Act
        var (http, body) = await s.Org.Owner.Client.GetAsync<List<AsoImpactEventDto>>(Url(s, "/events"));
        var (playOnly, playBody) = await s.Org.Owner.Client.GetAsync<List<AsoImpactEventDto>>(Url(s, "/events", $"&store={(int)StoreKind.Google}"));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        var events = body.Data;
        events.Select(e => e.Id).Should().BeEquivalentTo([$"plan-{s.LivePlanId}", $"plan-{s.AwaitingPlanId}", $"plan-{s.PlayListingPlanId}", $"plan-{s.PlayIconPlanId}", $"plan-{s.MsPlanId}", $"version-{s.VersionStateId}", $"ms-{s.MsSubmissionId}",
                                                          $"experiment-{s.ExperimentId}-start", $"experiment-{s.ExperimentId}-end", $"plan-{s.WinnerPlanId}"],
                                                         "discarded plans and plans that do not change the store page are left out");
        events.Select(e => e.Date).Should().BeInDescendingOrder();

        var live = events.Single(e => e.Id == $"plan-{s.LivePlanId}");
        live.Date.Should().Be(_effect, "the listing counts from the day its version went live, not the day it was written");
        live.DateSource.Should().Be(ImpactDateSource.StoreLive);
        live.Kind.Should().Be(ImpactEventKind.Listing);
        live.Locales.Should().Equal("tr");
        live.Fields.Should().Equal("subtitle");
        live.PlanId.Should().Be(s.LivePlanId);
        live.AppName.Should().Be("Pusula");

        events.Single(e => e.Id == $"plan-{s.AwaitingPlanId}").DateSource.Should().Be(ImpactDateSource.AwaitingRelease);
        events.Single(e => e.Id == $"plan-{s.PlayIconPlanId}").Kind.Should().Be(ImpactEventKind.Icon);
        events.Single(e => e.Id == $"version-{s.VersionStateId}").Kind.Should().Be(ImpactEventKind.VersionReleased);

        var ms = events.Single(e => e.Id == $"plan-{s.MsPlanId}");
        ms.Date.Should().Be(_effect.AddDays(-20), "a Microsoft Store change is live when the next submission is published");
        ms.DateSource.Should().Be(ImpactDateSource.StoreLive);

        var started = events.Single(e => e.Id == $"experiment-{s.ExperimentId}-start");
        started.Kind.Should().Be(ImpactEventKind.ExperimentStarted);
        started.Date.Should().Be(_effect.AddDays(-35));
        started.DateSource.Should().Be(ImpactDateSource.Experiment);
        started.Title.Should().Be("Yeni ikon testi");
        events.Single(e => e.Id == $"experiment-{s.ExperimentId}-end").Date.Should().Be(_effect.AddDays(-26));
        events.Single(e => e.Id == $"plan-{s.WinnerPlanId}").Kind.Should().Be(ImpactEventKind.ExperimentWinnerApplied);

        playOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        playBody.Data.Should().OnlyContain(e => e.Store == StoreKind.Google).And.HaveCount(2);
    }

    [Fact]
    public async Task Impact_ShouldCompareTheWindowsWithRanksAndTheRelatedRelease()
    {
        // Arrange
        var s = await SeedAsync();

        // Act
        var (http, body) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}"));
        var (_, week) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}&windowDays=7&gapDays=0&includeRanks=false"));
        var (_, us) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}&country=us"));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        var report = body.Data;
        report.Status.Should().Be(ImpactStatus.Ready);
        report.WindowDays.Should().Be(14);
        report.GapDays.Should().Be(2);
        report.WholeWeeks.Should().BeTrue();
        report.Before.From.Should().Be(_effect.AddDays(-14));
        report.Before.To.Should().Be(_effect.AddDays(-1));
        report.After.From.Should().Be(_effect.AddDays(2));
        report.After.To.Should().Be(_effect.AddDays(15));
        report.Before.DaysWithData.Should().Be(14);
        report.After.DaysWithData.Should().Be(14);

        var downloads = report.Metrics.Single(m => m.Metric == StoreMetricKind.Downloads);
        downloads.Before.Should().Be(100);
        downloads.After.Should().Be(150, "the gap's spike is skipped");
        downloads.RelativeChange.Should().BeApproximately(0.5, 1e-9);
        report.Metrics.Single(m => m.Metric == StoreMetricKind.Impressions).RelativeChange.Should().Be(0);
        report.Metrics.Single(m => m.Metric == StoreMetricKind.ConversionRate).AbsoluteChange.Should().BeApproximately(0.05, 1e-9);
        report.Conversion.Signal.Should().Be(ImpactSignal.Significant);
        report.Conversion.BeforePageViews.Should().Be(14000);
        report.BeforeDays.Should().HaveCount(14);
        report.AfterDays.Should().HaveCount(14);

        report.Related.Select(e => e.Id).Should().Equal($"version-{s.VersionStateId}");
        report.Confounders.Should().BeEmpty("the Play and Microsoft changes are other store records");

        report.Ranks.Compared.Should().Be(1);
        report.Ranks.BeforeAverage.Should().Be(15);
        report.Ranks.AfterAverage.Should().Be(8);
        report.Ranks.AfterTop10.Should().Be(1);
        report.Ranks.Improved.Should().Be(1);
        report.Ranks.Keywords.Should().ContainSingle().Which.Change.Should().Be(-7);

        week.Data.After.From.Should().Be(_effect, "gap 0 starts on the effect day");
        week.Data.Ranks.Should().BeNull();
        us.Data.Status.Should().Be(ImpactStatus.NoData);
        us.Data.Ranks.KeywordCount.Should().Be(0, "the country filter limits the keywords too");
    }

    [Fact]
    public async Task Impact_ShouldFlagConfoundingAndWaitForTheRelease()
    {
        var s = await SeedAsync();

        var (_, play) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.PlayListingPlanId}"));
        var (_, awaiting) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.AwaitingPlanId}"));

        var (_, winner) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.WinnerPlanId}"));

        play.Data.Confounders.Select(e => e.Id).Should().Equal($"plan-{s.PlayIconPlanId}");
        winner.Data.Confounders.Select(e => e.Id).Should().Equal([$"experiment-{s.ExperimentId}-start", $"experiment-{s.ExperimentId}-end"], "a test running in the before window splits the page's traffic");
        play.Data.Metrics.Single(m => m.Metric == StoreMetricKind.Impressions).Before.Should().BeNull("Google Play reports no impressions");
        play.Data.Conversion.Signal.Should().Be(ImpactSignal.NotSignificant);
        awaiting.Data.Status.Should().Be(ImpactStatus.AwaitingRelease);
    }

    [Fact]
    public async Task Recent_ShouldSummarizeTheOrganizationsLiveChanges()
    {
        var s = await SeedAsync();

        var (http, body) = await s.Org.Owner.Client.GetAsync<AsoImpactRecentDto>($"aso-impact/recent?organizationId={s.Org.OrganizationId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        body.Data.WindowDays.Should().Be(14);
        body.Data.Items.Select(i => i.Event.Id).Should().NotContain($"plan-{s.AwaitingPlanId}", "a change that is not live has no impact yet");
        var apple = body.Data.Items.Single(i => i.Event.Id == $"plan-{s.LivePlanId}");
        apple.Event.AppName.Should().Be("Pusula");
        apple.Status.Should().Be(ImpactStatus.Ready);
        apple.Downloads.RelativeChange.Should().BeApproximately(0.5, 1e-9);
        apple.Signal.Should().Be(ImpactSignal.Significant);
        apple.Confounded.Should().BeFalse();
        body.Data.Items.Single(i => i.Event.Id == $"plan-{s.PlayListingPlanId}").Confounded.Should().BeTrue();
        body.Data.Items.Should().NotContain(i => i.Event.Id == $"plan-{s.MsPlanId}", "older than 60 days");
    }

    [Fact]
    public async Task Impact_ShouldStayInsideTheOrganizationAndValidate()
    {
        var s = await SeedAsync();
        var other = await _factory.NewOrgAsync();

        var (foreign, _) = await other.Owner.Client.GetAsync<AsoImpactReportDto>($"aso-impact?organizationId={other.OrganizationId}&appId={s.AppId}&eventId=plan-{s.LivePlanId}");
        var (foreignEvents, _) = await other.Owner.Client.GetAsync<List<AsoImpactEventDto>>($"aso-impact/events?organizationId={other.OrganizationId}&appId={s.AppId}");
        var (notMember, _) = await other.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}"));
        var (unknown, unknownBody) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", "&eventId=plan-999999999"));
        var (window, _) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}&windowDays=2"));
        var (gap, _) = await s.Org.Owner.Client.GetAsync<AsoImpactReportDto>(Url(s, "", $"&eventId=plan-{s.LivePlanId}&gapDays=30"));
        var (recentForeign, recentBody) = await other.Owner.Client.GetAsync<AsoImpactRecentDto>($"aso-impact/recent?organizationId={other.OrganizationId}");

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignEvents.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notMember.StatusCode.Should().Be(HttpStatusCode.NotFound, "organization ids cannot be probed");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound, unknownBody?.MessagesToString());
        window.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        gap.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        recentForeign.StatusCode.Should().Be(HttpStatusCode.OK);
        recentBody.Data.Items.Should().BeEmpty();
    }
}
