using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Store page metrics: sync of the three stores, idempotent upserts, slices, enabling the App Store reports, the Play bucket, access problems, isolation.")]
public class StoreMetricsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly DateOnly _today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly _day = _today.AddDays(-3);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// One app with an App Store and a Google Play record (the org's connections).
    /// </summary>
    private async Task<(int AppId, int AppleRecordId, string AppleId, int PlayRecordId, string Package)> AddBothStoresAppAsync(EventOrg org, string name = "Pusula")
    {
        var appleId = $"6{Random.Shared.Next(100000, 999999)}{org.Suffix}";
        var package = $"com.{org.Suffix}.pusula{Random.Shared.Next(1000, 9999)}";
        var apple = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Apple, ExternalId = appleId, Name = name, StoreConnectionId = org.AppleConnectionId, SyncedDate = DateTime.UtcNow };
        var play = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Google, ExternalId = package, Name = name, StoreConnectionId = org.GoogleConnectionId, SyncedDate = DateTime.UtcNow };
        var app = new App { OrganizationId = org.OrganizationId, Name = name, StoreApps = [apple, play], CreationDate = DateTime.UtcNow };

        await _factory.DbAsync(async db =>
        {
            db.Apps.Add(app);
            await db.SaveChangesAsync();
        });

        return (app.Id, apple.Id, appleId, play.Id, package);
    }

    private static List<StoreMetricRow> AppleRows()
    {
        var rows = new StoreMetricAccumulator();

        rows.Add(_day, "TR", TrafficSource.Search, StoreMetricKind.Impressions, 1000);
        rows.Add(_day, "TR", TrafficSource.Search, StoreMetricKind.PageViews, 100);
        rows.Add(_day, "TR", TrafficSource.Search, StoreMetricKind.Downloads, 10);
        rows.Add(_day, "TR", TrafficSource.Search, StoreMetricKind.Redownloads, 2);
        rows.Add(_day, "US", TrafficSource.Browse, StoreMetricKind.Impressions, 500);
        rows.Add(_day, "US", TrafficSource.Browse, StoreMetricKind.PageViews, 50);
        rows.Add(_day, "US", TrafficSource.Browse, StoreMetricKind.Downloads, 5);

        // The day before, for the change against the previous period.
        rows.Add(_day.AddDays(-1), "TR", TrafficSource.Search, StoreMetricKind.PageViews, 110);
        rows.Add(_day.AddDays(-1), "TR", TrafficSource.Search, StoreMetricKind.Downloads, 11);

        return rows.ToRows();
    }

    // Google Play: countries carry the totals, the traffic source file only the source slices.
    private static List<StoreMetricRow> PlayRows()
    {
        var rows = new StoreMetricAccumulator();

        rows.Add(_day, "TR", null, StoreMetricKind.PageViews, 200);
        rows.Add(_day, "TR", null, StoreMetricKind.Downloads, 40);
        rows.Add(_day, null, TrafficSource.Search, StoreMetricKind.PageViews, 150, includeInTotal: false);
        rows.Add(_day, null, TrafficSource.Search, StoreMetricKind.Downloads, 30, includeInTotal: false);
        rows.Add(_day, null, TrafficSource.Browse, StoreMetricKind.PageViews, 50, includeInTotal: false);
        rows.Add(_day, null, TrafficSource.Browse, StoreMetricKind.Downloads, 10, includeInTotal: false);

        return rows.ToRows();
    }

    private async Task SyncAsync(EventOrg org, int appId)
    {
        var (http, jobs) = await org.Owner.Client.PostAsync<List<long>>("store-metrics/sync", new { organizationId = org.OrganizationId, appId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var jobId in jobs.Data)
            (await _factory.WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private async Task WaitForQueuedMetricJobsAsync(int organizationId)
    {
        var ids = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().Where(j => j.OrganizationId == organizationId && j.Kind == StoreJobKind.SyncStoreMetrics).Select(j => j.Id).ToListAsync());

        foreach (var id in ids)
            await _factory.WaitForJobAsync(id);
    }

    private Task<(HttpResponseMessage Http, Milvasoft.Components.Rest.MilvaResponse.Response<StoreMetricsReportDto> Body)> ReportAsync(EventOrg org, int appId, string extra = "")
        => org.Owner.Client.GetAsync<StoreMetricsReportDto>($"store-metrics?organizationId={org.OrganizationId}&appId={appId}&from={Day(_day)}&to={Day(_day)}{extra}");

    [Fact]
    public async Task Sync_ShouldStoreBothStoresIdempotentlyAndAnswerEverySliceOnce()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var other = await _factory.NewOrgAsync();
        var app = await AddBothStoresAppAsync(org);
        var requestId = $"rr-existing-{org.Suffix}";
        _factory.StoreMetrics.AppleRequests[app.AppleId] = new AppleReportRequest(requestId, false);
        _factory.StoreMetrics.AppleBatches[requestId] = new StoreMetricBatch(AppleRows(), _day.AddDays(1));
        _factory.StoreMetrics.PlayRows[app.Package] = PlayRows();
        await org.Owner.Client.PutAsync<string>("store-metrics/google/bucket", new { organizationId = org.OrganizationId, connectionId = org.GoogleConnectionId, bucket = "gs://pubsite_prod_rev_01234567890987654321/" });
        await WaitForQueuedMetricJobsAsync(org.OrganizationId);

        // Act
        await SyncAsync(org, app.AppId);
        var rowsAfterFirst = await _factory.DbAsync(db => db.StoreMetrics.CountAsync(m => m.OrganizationId == org.OrganizationId));
        await SyncAsync(org, app.AppId);
        var rowsAfterSecond = await _factory.DbAsync(db => db.StoreMetrics.CountAsync(m => m.OrganizationId == org.OrganizationId));

        var (_, report) = await ReportAsync(org, app.AppId);
        var (_, turkey) = await ReportAsync(org, app.AppId, "&country=tr");
        var (_, playOnly) = await ReportAsync(org, app.AppId, $"&store={(int)StoreKind.Google}");
        var (_, feeds) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={app.AppId}");
        var (foreign, _) = await other.Owner.Client.GetAsync<StoreMetricsReportDto>($"store-metrics?organizationId={other.OrganizationId}&appId={app.AppId}");
        var (foreignFeeds, _) = await other.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={other.OrganizationId}&appId={app.AppId}");
        var (reversed, _) = await org.Owner.Client.GetAsync<StoreMetricsReportDto>($"store-metrics?organizationId={org.OrganizationId}&appId={app.AppId}&from={Day(_day)}&to={Day(_day.AddDays(-5))}");

        // Assert
        rowsAfterFirst.Should().BeGreaterThan(0);
        rowsAfterSecond.Should().Be(rowsAfterFirst, "a second read of the same days updates the rows instead of adding to them");
        _factory.StoreMetrics.CreatedRequests.Should().NotContain(app.AppleId, "a sync only reads; an existing request is adopted");
        _factory.StoreMetrics.Reads.Should().Contain(r => r.AppId == app.Package && r.Key == "pubsite_prod_rev_01234567890987654321");

        var data = report.Data;
        data.Totals.Impressions.Should().Be(1500, "only the App Store reports impressions");
        data.Totals.PageViews.Should().Be(350);
        data.Totals.Downloads.Should().Be(55, "Play's source file does not count twice");
        data.Totals.Redownloads.Should().Be(2);
        data.Totals.ConversionRate.Should().BeApproximately(55d / 350, 0.000001);
        data.PreviousTotals.Downloads.Should().Be(11);
        data.Changes.Downloads.Should().BeApproximately(4, 0.000001);
        data.Days.Should().ContainSingle(d => d.Date == _day && d.Downloads == 55);
        data.Sources.Should().ContainSingle(s => s.Source == TrafficSource.Search).Which.Totals.Should().Match<StoreMetricValuesDto>(t => t.PageViews == 250 && t.Downloads == 40);
        data.Sources.Should().ContainSingle(s => s.Source == TrafficSource.Browse).Which.Totals.Downloads.Should().Be(15);
        data.Countries.Select(c => c.Country).Should().Equal("TR", "US");
        data.Stores.Should().ContainSingle(s => s.Store == StoreKind.Google).Which.Totals.Should().Match<StoreMetricValuesDto>(t => t.Impressions == null && t.PageViews == 200 && t.Downloads == 40);

        turkey.Data.Totals.Should().Match<StoreMetricValuesDto>(t => t.Impressions == 1000 && t.PageViews == 300 && t.Downloads == 50);
        turkey.Data.Sources.Should().ContainSingle("Play reports countries and sources separately, so only the App Store has TR by source").Which.Source.Should().Be(TrafficSource.Search);
        playOnly.Data.Totals.Impressions.Should().BeNull();

        feeds.Data.Should().HaveCount(2);
        feeds.Data.Should().ContainSingle(f => f.Store == StoreKind.Apple).Which.Should().Match<StoreMetricFeedDto>(f => f.Status == MetricFeedStatus.Active && !f.CanEnable && f.LatestDataDate == _day);
        feeds.Data.Should().ContainSingle(f => f.Store == StoreKind.Google).Which.Should().Match<StoreMetricFeedDto>(f => f.Status == MetricFeedStatus.Active && f.ReportBucket == "pubsite_prod_rev_01234567890987654321");

        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreignFeeds.StatusCode.Should().Be(HttpStatusCode.NotFound);
        reversed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EnableAppleAnalytics_ShouldNeedTheConnectionPermissionAndAConfirmationAndBeAudited()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, recordId, appleId) = await _factory.AddAppAsync(org, StoreKind.Apple, "Mola");
        var (_, playRecordId, _) = await _factory.AddAppAsync(org, StoreKind.Google, "Mola Android");
        var editor = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Editor);

        // Act
        var (_, before) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={appId}");
        await SyncAsync(org, appId);
        var (_, afterSync) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={appId}");
        var (byEditor, _) = await editor.Client.PostAsync<long>("store-metrics/apple/enable", new { organizationId = org.OrganizationId, storeAppId = recordId, confirm = true });
        var (unconfirmed, _) = await org.Owner.Client.PostAsync<long>("store-metrics/apple/enable", new { organizationId = org.OrganizationId, storeAppId = recordId, confirm = false });
        var (forPlay, _) = await org.Owner.Client.PostAsync<long>("store-metrics/apple/enable", new { organizationId = org.OrganizationId, storeAppId = playRecordId, confirm = true });
        var (_, job) = await org.Owner.Client.PostAsync<long>("store-metrics/apple/enable", new { organizationId = org.OrganizationId, storeAppId = recordId, confirm = true });
        (await _factory.WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
        await WaitForQueuedMetricJobsAsync(org.OrganizationId);
        var (again, _) = await org.Owner.Client.PostAsync<long>("store-metrics/apple/enable", new { organizationId = org.OrganizationId, storeAppId = recordId, confirm = true });
        var (_, after) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={appId}");
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.AppleAnalyticsEnabled).ToListAsync());

        // Assert
        before.Data.Should().ContainSingle().Which.Should().Match<StoreMetricFeedDto>(f => f.Status == MetricFeedStatus.NotEnabled && f.CanEnable);
        afterSync.Data.Single().Status.Should().Be(MetricFeedStatus.NotEnabled, "a sync never creates the request");
        byEditor.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        forPlay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.StoreMetrics.CreatedRequests.Count(id => id == appleId).Should().Be(1);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        after.Data.Single().Should().Match<StoreMetricFeedDto>(f => f.Status == MetricFeedStatus.Waiting && !f.CanEnable && f.EnabledByUserName != null && f.EnabledDate != null);
        audit.Should().ContainSingle().Which.TargetId.Should().Be(recordId.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task GooglePlay_ShouldAskForTheBucketAndReportARefusedBucketAsAConnectionProblem()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var (appId, _, package) = await _factory.AddAppAsync(org, StoreKind.Google, "Defter");
        var editor = await _factory.AddMemberAsync(org.OrganizationId, OrgRole.Editor);
        _factory.StoreMetrics.Failures[package] = new StoreCallException("403: store@demo.iam.gserviceaccount.com does not have storage.objects.get access", 403, false);

        // Act
        await SyncAsync(org, appId);
        var (_, needsSetup) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={appId}");
        var (invalid, _) = await org.Owner.Client.PutAsync<string>("store-metrics/google/bucket", new { organizationId = org.OrganizationId, connectionId = org.GoogleConnectionId, bucket = "not a bucket!" });
        var (onApple, _) = await org.Owner.Client.PutAsync<string>("store-metrics/google/bucket", new { organizationId = org.OrganizationId, connectionId = org.AppleConnectionId, bucket = "pubsite_prod_rev_1" });
        var (byEditor, _) = await editor.Client.PutAsync<string>("store-metrics/google/bucket", new { organizationId = org.OrganizationId, connectionId = org.GoogleConnectionId, bucket = "pubsite_prod_rev_1" });
        var (_, set) = await org.Owner.Client.PutAsync<string>("store-metrics/google/bucket", new { organizationId = org.OrganizationId, connectionId = org.GoogleConnectionId, bucket = "gs://pubsite_prod_rev_98765/stats/installs" });
        await WaitForQueuedMetricJobsAsync(org.OrganizationId);
        await SyncAsync(org, appId);
        var (_, refused) = await org.Owner.Client.GetAsync<List<StoreMetricFeedDto>>($"store-metrics/feeds?organizationId={org.OrganizationId}&appId={appId}");
        var bucketAudit = await _factory.DbAsync(db => db.StoreAuditLogs.CountAsync(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.ReportBucketUpdated));

        // Assert
        needsSetup.Data.Single().Status.Should().Be(MetricFeedStatus.NeedsSetup);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        onApple.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        byEditor.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        set.Data.Should().Be("pubsite_prod_rev_98765");
        refused.Data.Single().Should().Match<StoreMetricFeedDto>(f => f.Status == MetricFeedStatus.AccessDenied && f.LastError.Contains("403") && f.ReportBucket == "pubsite_prod_rev_98765");
        bucketAudit.Should().Be(1);
    }

    [Fact]
    public async Task Microsoft_ShouldBackfillNinetyDaysThenReadTheLastWeekAgain()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var tenant = Guid.NewGuid().ToString();
        var secret = JsonSerializer.Serialize(new { tenantId = tenant, clientId = "0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12", clientSecret = "client-secret-value", sellerId = "40291187" });
        var (_, connection) = await org.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = org.OrganizationId, store = StoreKind.Microsoft, name = "Partner Center", secret, credentialExpiryDate = DateTime.UtcNow.AddMonths(12) });
        var productId = $"9N{Random.Shared.Next(10000000, 99999999)}";
        var record = new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Microsoft, ExternalId = productId, Name = "Pusula", StoreConnectionId = connection.Data, SyncedDate = DateTime.UtcNow };
        var app = new App { OrganizationId = org.OrganizationId, Name = "Pusula", StoreApps = [record], CreationDate = DateTime.UtcNow };
        await _factory.DbAsync(async db =>
        {
            db.Apps.Add(app);
            await db.SaveChangesAsync();
        });

        var rows = new StoreMetricAccumulator();
        rows.Add(_day, "TR", TrafficSource.StoreClient, StoreMetricKind.Downloads, 7);
        rows.Add(_day, "DE", TrafficSource.StoreWeb, StoreMetricKind.Downloads, 3);
        rows.Add(_day, null, null, StoreMetricKind.PageViews, 40);
        _factory.StoreMetrics.MicrosoftRows[productId] = rows.ToRows();

        // Act
        await SyncAsync(org, app.Id);
        await SyncAsync(org, app.Id);
        var reads = _factory.StoreMetrics.Reads.Where(r => r.AppId == productId).ToList();
        var (_, report) = await ReportAsync(org, app.Id);

        // Assert
        reads.Should().HaveCount(2);
        reads[0].From.Should().Be(_today.AddDays(-90));
        reads[0].To.Should().Be(_today.AddDays(-1));
        reads[1].From.Should().Be(_day.AddDays(-7), "later reads repeat the last week of data");
        report.Data.Totals.Should().Match<StoreMetricValuesDto>(t => t.Downloads == 10 && t.PageViews == 40 && t.Impressions == null);
        report.Data.Totals.ConversionRate.Should().BeApproximately(0.25, 0.000001);
        report.Data.Sources.Select(s => s.Source).Should().BeEquivalentTo([TrafficSource.StoreClient, TrafficSource.StoreWeb]);
    }

    [Fact]
    public async Task SummaryAndQueryService_ShouldSumPerAppAndReturnASeries()
    {
        // Arrange
        var org = await _factory.NewOrgAsync();
        var first = await AddBothStoresAppAsync(org, "Pusula");
        var second = await AddBothStoresAppAsync(org, "Defter");
        foreach (var app in new[] { first, second })
        {
            var requestId = $"rr-{app.AppleId}";
            _factory.StoreMetrics.AppleRequests[app.AppleId] = new AppleReportRequest(requestId, false);
            _factory.StoreMetrics.AppleBatches[requestId] = new StoreMetricBatch(app.AppId == first.AppId ? AppleRows() : [], _day);
        }

        await SyncAsync(org, first.AppId);
        await SyncAsync(org, second.AppId);

        // Act
        var (_, summary) = await org.Owner.Client.GetAsync<StoreMetricsSummaryDto>($"store-metrics/summary?organizationId={org.OrganizationId}&from={Day(_day)}&to={Day(_day)}");
        using var scope = _factory.Services.CreateScope();
        var series = await scope.ServiceProvider.GetRequiredService<IStoreMetricsQuery>()
                                                .GetMetricSeriesAsync(new StoreMetricScope(org.OrganizationId, first.AppId), StoreMetricKind.ConversionRate, _day.AddDays(-1), _day, TestContext.Current.CancellationToken);

        // Assert
        summary.Data.Apps.Should().ContainSingle(a => a.AppId == first.AppId).Which.Totals.Downloads.Should().Be(15);
        summary.Data.Apps.Should().NotContain(a => a.AppId == second.AppId, "an app without data in either period is left out");
        summary.Data.Totals.Downloads.Should().Be(15);
        series.Select(p => p.Date).Should().Equal(_day.AddDays(-1), _day);
        series[0].Value.Should().BeApproximately(0.1, 0.000001);
        series[1].Value.Should().BeApproximately(0.1, 0.000001);
    }
}
