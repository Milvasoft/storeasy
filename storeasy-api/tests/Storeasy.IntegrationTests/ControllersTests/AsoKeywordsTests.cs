using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.Infrastructure.StoreJobs;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "ASO keywords: research from the store sources, tracking with its limit and audit, the daily rank job, rank history and the keywords field helper.")]
public class AsoKeywordsTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private FakeAsoStores Stores => _factory.AsoStores;

    private async Task<(Org Org, Record Record, string Keyword)> AppleAppAsync()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        _factory.Stores.Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        return (org, record, $"compass {org.Suffix}");
    }

    private static object ResearchRequest(Org org, int appId, StoreKind store, string country, params string[] keywords)
        => new { organizationId = org.OrganizationId, appId, store, country, keywords };

    private async Task<long> QueueResearchAsync(TestUser user, object request)
    {
        var (http, body) = await user.Client.PostAsync<long>("aso-keywords/research", request);

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        body.Data.Should().BePositive();

        return body.Data;
    }

    private Task<(HttpResponseMessage Http, Milvasoft.Components.Rest.MilvaResponse.Response<AsoKeywordResearchJobDto> Body)> ReadResearchAsync(TestUser user, Org org, int appId, StoreKind store, long? jobId = null)
        => user.Client.GetAsync<AsoKeywordResearchJobDto>($"aso-keywords/research?organizationId={org.OrganizationId}&appId={appId}&store={store}{(jobId == null ? string.Empty : $"&jobId={jobId}")}");

    private async Task<(StoreJob Job, AsoKeywordResearchJobDto Research)> ResearchAndWaitAsync(TestUser user, Org org, int appId, StoreKind store, string country, params string[] keywords)
    {
        var jobId = await QueueResearchAsync(user, ResearchRequest(org, appId, store, country, keywords));
        var job = await WaitForJobAsync(jobId);
        var (http, body) = await ReadResearchAsync(user, org, appId, store, jobId);

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());

        return (job, body.Data);
    }

    private async Task WaitForRequestAsync(string term)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);

        while (!Stores.Requests.Any(r => r.Query.GetValueOrDefault("term") == term))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"No store request for '{term}'.");

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Research_ShouldQueueAJob_ThatScoresFromTheStoreData_AndFindsTheAppsRank()
    {
        // Arrange
        var (org, record, keyword) = await AppleAppAsync();
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        Stores.Hints[keyword] = [keyword, $"{keyword} app"];
        Stores.Results[keyword] = [("2001", "Compass Pro", 120000, 4.8), (record.ExternalId, "Pusula", 40, 4.0), ("2003", "Maps", 5000, 4.4)];
        var (_, before) = await ReadResearchAsync(viewer, org, record.AppId, StoreKind.Apple);

        // Act
        var (job, research) = await ResearchAndWaitAsync(viewer, org, record.AppId, StoreKind.Apple, "TR", keyword.ToUpperInvariant(), $"{keyword} pro");
        var (_, latest) = await ReadResearchAsync(viewer, org, record.AppId, StoreKind.Apple);
        await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "tr", keywords = new[] { keyword } });
        var (_, afterTracking) = await ReadResearchAsync(viewer, org, record.AppId, StoreKind.Apple, job.Id);
        var metric = await _factory.DbAsync(db => db.AsoKeywordMetrics.AsNoTracking().SingleAsync(m => m.Keyword == keyword && m.Country == "tr"));
        var steps = await _factory.DbAsync(db => db.StoreJobSteps.AsNoTracking().Where(s => s.StoreJobId == job.Id).ToListAsync());

        // Assert
        before.Data.Should().BeNull("the app had no research yet");
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        job.Kind.Should().Be(StoreJobKind.AsoKeywordResearch);
        job.AppId.Should().Be(record.AppId);
        steps.Should().ContainSingle(s => s.Name == AsoKeywordResearchResult.MeasureStep).Which.Should().Match<StoreJobStep>(s => s.Progress == 2 && s.Total == 2 && s.Status == StoreJobStepStatus.Succeeded);
        research.Should().Match<AsoKeywordResearchJobDto>(r => r.JobId == job.Id && r.Status == StoreJobStatus.Succeeded && r.Country == "tr" && r.Measured == 2 && r.Total == 2 && r.Error == null);
        research.Keywords.Should().Equal(keyword, $"{keyword} pro");
        research.Items.Select(i => i.Keyword).Should().Equal(keyword, $"{keyword} pro");
        var item = research.Items[0];
        item.Popularity.Should().BeInRange(10, 100);
        item.PopularitySource.Should().Be("apple-search-hints");
        item.Difficulty.Should().BeInRange(1, 100);
        item.Opportunity.Should().NotBeNull();
        item.CompetingApps.Should().Be(3);
        item.CurrentRank.Should().Be(2);
        item.RankChecked.Should().BeTrue();
        item.Tracked.Should().BeFalse();
        item.TopCompetitors.Select(c => c.Id).Should().Equal("2001", record.ExternalId, "2003");
        item.TopCompetitors[1].IsOwnApp.Should().BeTrue();
        item.RelatedTerms.Should().Contain($"{keyword} app");
        metric.Popularity.Should().Be(item.Popularity, "the measurement is saved for the next research");
        latest.Data.JobId.Should().Be(job.Id, "without a job id the latest research of the app answers");
        afterTracking.Data.Items.Single(i => i.Keyword == keyword).Tracked.Should().BeTrue("the tracking state is read with the result");
        Stores.Requests.Should().Contain(r => r.Host == "search.itunes.apple.com" && r.Query["term"] == keyword && r.Storefront == "143480-1,29" && r.UserAgent == AsoOptions.DefaultUserAgent);
        Stores.Requests.Should().Contain(r => r.Host == "itunes.apple.com" && r.Query["term"] == keyword && r.Query["country"] == "tr" && r.Query["limit"] == "200");
    }

    [Fact]
    public async Task Research_OnGooglePlay_ShouldScorePopularityFromTheSearchBoxSuggestions_WithoutReadingSearchPages()
    {
        var org = await NewOrgAsync();
        var (_, google) = await NewAppAsync(org, null, package: $"com.{org.Suffix}.pusula");
        var keyword = $"qibla {org.Suffix}";
        Stores.Hints[keyword] = [keyword, $"{keyword} app"];

        var (job, research) = await ResearchAndWaitAsync(org.Owner, org, google.AppId, StoreKind.Google, "us", keyword);

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        research.Items.Should().ContainSingle().Which.Should().Match<AsoKeywordResearchDto>(k => k.Popularity >= 10 && k.PopularitySource == "google-play-suggest" && !k.RankChecked && k.Difficulty == null);
        Stores.Requests.Should().Contain(r => r.Host == "play.google.com" && r.Path == "/_/PlayStoreUi/data/batchexecute" && r.Query["rpcids"] == "IJ4APc" && r.Query["gl"] == "US");
        Stores.Requests.Should().NotContain(r => r.Host == "play.google.com" && r.Path == "/store/search", "reading Play search pages is off");
    }

    [Fact]
    public async Task Research_WhenTheStoreIsDown_ShouldFailTheJobWithAClearMessage_AndBadInputIsRefusedAtOnce()
    {
        var (org, record, _) = await AppleAppAsync();
        var other = await NewOrgAsync();

        var (job, research) = await ResearchAndWaitAsync(org.Owner, org, record.AppId, StoreKind.Apple, "us", $"{FakeAsoStores.OfflineMarker} {org.Suffix}");
        var (badCountry, _) = await org.Owner.Client.PostAsync<long>("aso-keywords/research", ResearchRequest(org, record.AppId, StoreKind.Apple, "TUR", "compass"));
        var (noKeywords, _) = await org.Owner.Client.PostAsync<long>("aso-keywords/research", ResearchRequest(org, record.AppId, StoreKind.Apple, "us"));
        var (foreign, _) = await ReadResearchAsync(other.Owner, other, record.AppId, StoreKind.Apple, job.Id);

        job.Status.Should().Be(StoreJobStatus.Failed);
        job.Attempt.Should().Be(1, "a store that did not answer is not retried in a loop");
        research.Status.Should().Be(StoreJobStatus.Failed);
        research.Error.Should().NotBeNullOrWhiteSpace().And.NotBe(AsoKeywordResearchResult.SourceUnavailableError, "the known reason is shown localized");
        research.Items.Should().BeEmpty();
        badCountry.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        noKeywords.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreign.StatusCode.Should().NotBe(HttpStatusCode.OK, "another organization cannot read the research");
    }

    [Fact]
    public async Task Research_WhileRunning_TheSameRequestShouldAnswerTheSameJob_AndCancelShouldStopIt()
    {
        // Arrange: the first keyword's store call waits, so the job stays running.
        var (org, record, keyword) = await AppleAppAsync();
        var slow = $"slow {keyword}";
        var next = $"next {keyword}";
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Stores.Gates[slow] = gate;

        try
        {
            // Act
            var jobId = await QueueResearchAsync(org.Owner, ResearchRequest(org, record.AppId, StoreKind.Apple, "us", slow, next));
            await WaitForRequestAsync(slow);
            var again = await QueueResearchAsync(org.Owner, ResearchRequest(org, record.AppId, StoreKind.Apple, "US", slow.ToUpperInvariant(), next));
            var (_, running) = await ReadResearchAsync(org.Owner, org, record.AppId, StoreKind.Apple);
            var (cancelHttp, cancel) = await org.Owner.Client.PostAsync<StoreJobStatus>("StoreJobs/job/cancel", new { organizationId = org.OrganizationId, jobId });

            // The cancel request is read at the next progress report (at most once a second).
            await Task.Delay(StoreJobContext.CancelCheckInterval + TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
            gate.SetResult();
            var job = await WaitForJobAsync(jobId);
            var (_, cancelled) = await ReadResearchAsync(org.Owner, org, record.AppId, StoreKind.Apple, jobId);

            // Assert
            again.Should().Be(jobId, "an identical research still on the way is not queued twice");
            running.Data.Should().Match<AsoKeywordResearchJobDto>(r => r.JobId == jobId && r.Status == StoreJobStatus.Running && r.Measured == 0 && r.Total == 2);
            cancelHttp.StatusCode.Should().Be(HttpStatusCode.OK, cancel?.MessagesToString());
            job.Status.Should().Be(StoreJobStatus.Cancelled);
            job.Result.Should().BeNull();
            cancelled.Data.Status.Should().Be(StoreJobStatus.Cancelled);
            cancelled.Data.Items.Should().BeEmpty();
            Stores.Requests.Should().NotContain(r => r.Query.GetValueOrDefault("term") == next, "the job stopped before the next keyword");
        }
        finally
        {
            gate.TrySetResult();
            Stores.Gates.TryRemove(slow, out _);
        }
    }

    [Fact]
    public async Task Track_ShouldNeedEditor_AuditAndListTheKeywords_ThenUntrackKeepingHistory()
    {
        // Arrange
        var (org, record, keyword) = await AppleAppAsync();
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var request = new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "tr", keywords = new[] { keyword, $"  {keyword.ToUpperInvariant()} " }, trackCompetitors = true };

        // Act
        var (viewerHttp, _) = await viewer.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", request);
        var (http, tracked) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", request);
        var (_, list) = await viewer.Client.GetAsync<List<AsoTrackedKeywordDto>>($"aso-keywords?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");
        var (untrackHttp, untracked) = await org.Owner.Client.PostAsync<int>("aso-keywords/untrack", new { organizationId = org.OrganizationId, ids = new[] { tracked.Data[0].Id } });
        var (_, afterUntrack) = await viewer.Client.GetAsync<List<AsoTrackedKeywordDto>>($"aso-keywords?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");
        var (retrackHttp, retracked) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", request);
        var audits = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && (a.Action == AuditAction.AsoKeywordsTracked || a.Action == AuditAction.AsoKeywordsUntracked)).ToListAsync());
        var rows = await _factory.DbAsync(db => db.AsoTrackedKeywords.AsNoTracking().Where(t => t.OrganizationId == org.OrganizationId).ToListAsync());

        // Assert
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        http.StatusCode.Should().Be(HttpStatusCode.OK, tracked?.MessagesToString());
        tracked.Data.Should().ContainSingle("the two spellings are one keyword").Which.Should().Match<AsoTrackedKeywordDto>(t => t.Keyword == keyword && t.Country == "tr" && t.TrackCompetitors && t.CurrentRank == null && t.Sparkline.Count == 30);
        list.Data.Should().ContainSingle();
        untrackHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        untracked.Data.Should().Be(1);
        afterUntrack.Data.Should().BeEmpty();
        retrackHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        retracked.Data.Should().ContainSingle().Which.Id.Should().Be(tracked.Data[0].Id, "tracking again continues the same row and history");
        rows.Should().ContainSingle().Which.IsActive.Should().BeTrue();
        audits.Select(a => a.Action).Should().BeEquivalentTo([AuditAction.AsoKeywordsTracked, AuditAction.AsoKeywordsUntracked, AuditAction.AsoKeywordsTracked]);
        audits.Should().OnlyContain(a => a.TargetId == record.StoreAppId.ToString() && a.Store == StoreKind.Apple);
    }

    [Fact]
    public async Task Track_ShouldStopAtTheOrganizationsLimit()
    {
        var (org, record, keyword) = await AppleAppAsync();
        var limit = _factory.Services.GetRequiredService<AsoOptions>().MaxTrackedKeywordsPerOrganization;

        await _factory.DbAsync(async db =>
        {
            db.AsoTrackedKeywords.AddRange(Enumerable.Range(0, limit).Select(i => new AsoTrackedKeyword
            {
                OrganizationId = org.OrganizationId,
                AppId = record.AppId,
                StoreAppId = record.StoreAppId,
                Store = StoreKind.Apple,
                Keyword = $"seeded {i}",
                Country = "us",
                IsActive = true,
                TrackedDate = DateTime.UtcNow,
                LastCheckedDate = DateTime.UtcNow,
            }));
            await db.SaveChangesAsync();
        });

        var (http, body) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", keywords = new[] { keyword } });
        var (again, _) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", keywords = new[] { "seeded 1" } });

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Messages.Should().Contain(m => m.Key == MessageKey.AsoKeywordLimitReached && m.Message.Contains(limit.ToString()));
        again.StatusCode.Should().Be(HttpStatusCode.OK, "a keyword already tracked does not count again");
    }

    [Fact]
    public async Task Track_GooglePlayWhileItsSwitchIsOff_ShouldBeRefused()
    {
        var org = await NewOrgAsync();
        var (_, google) = await NewAppAsync(org, null, package: $"com.{org.Suffix}.pusula");

        var (http, body) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = google.AppId, store = StoreKind.Google, country = "tr", keywords = new[] { "pusula" } });

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Messages.Should().Contain(m => m.Key == MessageKey.AsoGooglePlayRanksDisabled);
    }

    [Fact]
    public async Task RankJob_ShouldRecordOneRowPerDay_WithCompetitors_AndTheSeriesShouldReadIt()
    {
        // Arrange
        var (org, record, keyword) = await AppleAppAsync();
        Stores.Results[keyword] = [("3001", "Big Compass", 90000, 4.7), ("3002", "Small Compass", 300, 4.2), (record.ExternalId, "Pusula", 40, 4.0), ("3004", "Qibla", 10, 3.0)];
        var (_, tracked) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", keywords = new[] { keyword }, trackCompetitors = true });
        var id = tracked.Data[0].Id;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Yesterday the app was 5th.
        await _factory.DbAsync(async db =>
        {
            db.AsoKeywordRanks.Add(new AsoKeywordRank { OrganizationId = org.OrganizationId, TrackedKeywordId = id, StoreAppId = record.StoreAppId, Store = StoreKind.Apple, Keyword = keyword, Country = "us", Date = today.AddDays(-1), Rank = 5, Depth = 200, CheckedDate = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        });

        // Act: run until nothing is left for today (other tests' keywords share the database and the per run limit).
        var runs = new List<int>();

        do
        {
            using var scope = _factory.CreateScope();

            runs.Add(await scope.ServiceProvider.GetRequiredService<IAsoRankRecorder>().RecordDueAsync(TestContext.Current.CancellationToken));
        }
        while (runs[^1] > 0 && runs.Count < 20);

        var ranks = await _factory.DbAsync(db => db.AsoKeywordRanks.AsNoTracking().Where(r => r.TrackedKeywordId == id).OrderBy(r => r.Date).ToListAsync());
        var competitors = await _factory.DbAsync(db => db.AsoCompetitorRanks.AsNoTracking().Where(r => r.TrackedKeywordId == id).ToListAsync());
        var (_, list) = await org.Owner.Client.GetAsync<List<AsoTrackedKeywordDto>>($"aso-keywords?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}&country=US");
        var (detailHttp, detail) = await org.Owner.Client.GetAsync<AsoKeywordRanksDto>($"aso-keywords/ranks?organizationId={org.OrganizationId}&id={id}&days=7");
        AsoRankSeries series;

        using (var scope = _factory.CreateScope())
            series = await scope.ServiceProvider.GetRequiredService<IAsoRankSeries>().GetRankSeriesAsync(record.StoreAppId, keyword.ToUpperInvariant(), "US", today.AddDays(-2), today, TestContext.Current.CancellationToken);

        // Assert
        runs[0].Should().BeGreaterThanOrEqualTo(1);
        runs[^1].Should().Be(0, "today is already checked");
        ranks.Select(r => (r.Date, r.Rank)).Should().Equal((today.AddDays(-1), 5), (today, 3));
        ranks[1].Depth.Should().Be(4);
        competitors.Select(c => (c.CompetitorId, c.Rank)).Should().BeEquivalentTo([("3001", 1), ("3002", 2), ("3004", 4)], "the top results other than the app are followed by default");
        competitors.Should().Contain(c => c.CompetitorId == "3001" && c.CompetitorName == "Big Compass");
        var row = list.Data.Should().ContainSingle().Subject;
        row.Should().Match<AsoTrackedKeywordDto>(t => t.CurrentRank == 3 && t.Change1 == 2 && t.Change7 == null && t.Difficulty != null && t.CompetitorIds.Count == 3);
        row.Sparkline.TakeLast(2).Should().Equal(5, 3);
        detailHttp.StatusCode.Should().Be(HttpStatusCode.OK, detail?.MessagesToString());
        detail.Data.Points.Should().HaveCount(7);
        detail.Data.Points[^1].Should().Match<AsoRankPointDto>(p => p.Checked && p.Rank == 3);
        detail.Data.Points[0].Checked.Should().BeFalse();
        detail.Data.Competitors.Should().HaveCount(3);
        detail.Data.TopCompetitors.Should().Contain(c => c.IsOwnApp && c.Rank == 3);
        series.Points.Select(p => (p.Checked, p.Rank)).Should().Equal((false, (int?)null), (true, 5), (true, 3));
    }

    [Fact]
    public async Task RankJob_WhenTheSearchFails_ShouldLeaveTheKeywordForTheNextRun()
    {
        var (org, record, _) = await AppleAppAsync();
        var keyword = $"{FakeAsoStores.OfflineMarker} {org.Suffix}";
        var (_, tracked) = await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", keywords = new[] { keyword } });

        using (var scope = _factory.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAsoRankRecorder>().RecordDueAsync(TestContext.Current.CancellationToken);

        var row = await _factory.DbAsync(db => db.AsoTrackedKeywords.AsNoTracking().SingleAsync(t => t.Id == tracked.Data[0].Id));
        var ranks = await _factory.DbAsync(db => db.AsoKeywordRanks.AsNoTracking().CountAsync(r => r.TrackedKeywordId == row.Id));

        row.LastCheckedDate.Should().BeNull();
        ranks.Should().Be(0);
    }

    [Fact]
    public async Task Coverage_ShouldTellWhatTheAppleFieldsCover()
    {
        var (org, record, _) = await AppleAppAsync();
        await org.Owner.Client.PostAsync<List<AsoTrackedKeywordDto>>("aso-keywords/track", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", keywords = new[] { "compass", "offline maps" } });

        var (http, body) = await org.Owner.Client.GetAsync<List<AsoKeywordCoverageDto>>($"aso-keywords/coverage?organizationId={org.OrganizationId}&appId={record.AppId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        var locale = body.Data.Should().ContainSingle().Subject;
        locale.Should().Match<AsoKeywordCoverageDto>(c => c.Locale == "en-US" && c.Keywords == "compass,map" && c.KeywordsLength == 11 && c.Remaining == 89);
        locale.Items.Should().Contain(i => i.Keyword == "compass" && i.Covered);
        locale.Items.Should().Contain(i => i.Keyword == "offline maps" && !i.Covered && i.MissingWords.SequenceEqual(new[] { "offline", "maps" }));
        locale.CharactersNeeded.Should().Be(13);
    }

    [Fact]
    public async Task Seeds_ShouldComeFromTheListingCompetitorsAndSuggestions_AndAiIdeasAreAudited()
    {
        // Arrange
        var (org, record, _) = await AppleAppAsync();
        var key = FakeAnthropic.Key($"aso{org.Suffix}");
        await org.Owner.Client.PutAsync<AiSettingsDto>("organizations/ai-settings/key", new { organizationId = org.OrganizationId, apiKey = key });
        _factory.Anthropic.Replies[key] = (HttpStatusCode.OK, "end_turn", "{\"clusters\": [{\"name\": \"Navigation\", \"terms\": [\"Digital Compass\", \"compass\", \"qibla finder\"]}]}");

        // Act
        var (http, seeds) = await org.Owner.Client.GetAsync<AsoKeywordSeedsDto>($"aso-keywords/seeds?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}&country=us");
        var (aiHttp, ideas) = await org.Owner.Client.PostAsync<AsoKeywordSeedsDto>("aso-keywords/seeds/suggest", new { organizationId = org.OrganizationId, appId = record.AppId, store = StoreKind.Apple, country = "us", seeds = new[] { "compass" } });
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().SingleAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.AsoKeywordSeedsGenerated));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK, seeds?.MessagesToString());
        seeds.Data.Locale.Should().Be("en-US");
        seeds.Data.Seeds.Should().Contain(s => s.Term == "pusula" && s.Source == "name");
        seeds.Data.Seeds.Should().Contain(s => s.Term == "find your way" && s.Source == "subtitle");
        seeds.Data.Seeds.Should().Contain(s => s.Term == "compass" && s.Source == "keywords");
        seeds.Data.Seeds.Should().Contain(s => s.Source == "suggestions");
        aiHttp.StatusCode.Should().Be(HttpStatusCode.OK, ideas?.MessagesToString());
        ideas.Data.Seeds.Select(s => s.Term).Should().Equal("digital compass", "qibla finder");
        ideas.Data.Seeds.Should().OnlyContain(s => s.Source == "ai" && s.Cluster == "Navigation");
        audit.Details.Should().Contain("EstimatedCostUsd").And.NotContain("sk-ant");
    }
}
