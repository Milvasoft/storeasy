using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Chrome Web Store: connection, item sync, extension ZIP intake, submit, review state, rollout, cancel, rollback, reviews and metrics; against a fake Chrome Web Store API.")]
public class ChromeControllerTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private FakeChromeWebStore Chrome => _factory.ChromeWebStore;

    private sealed record Setup(TestUser Owner, int OrganizationId, int ConnectionId, string PublisherId, string ItemId, string RefreshToken);

    private static string Secret(string publisherId, string refreshToken, string itemId = null)
        => JsonSerializer.Serialize(new { publisherId, clientId = "1234567890-abcdef.apps.googleusercontent.com", clientSecret = "client-secret-value", refreshToken, itemId });

    private async Task<Setup> NewSetupAsync(string publishedVersion = "1.0.0", int deployPercentage = 100)
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (publisherId, itemId, refreshToken) = Chrome.AddItem(publishedVersion, deployPercentage);
        var (http, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new
        {
            organizationId = organization.Data,
            store = StoreKind.Chrome,
            name = "Chrome Web Store",
            secret = Secret(publisherId, refreshToken, itemId),
            icon = "chromewebstore",
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", connection.Messages?.Select(m => m.Message) ?? []));

        return new Setup(owner, organization.Data, connection.Data, publisherId, itemId, refreshToken);
    }

    private async Task<StoreJob> WaitForJobAsync(long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    private async Task<ChromeItemAddedDto> AddItemAsync(Setup setup)
    {
        var (http, added) = await setup.Owner.Client.PostAsync<ChromeItemAddedDto>("chrome/item", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, itemId = setup.ItemId, name = "Kabuk Tabs" });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", added.Messages?.Select(m => m.Message) ?? []));
        (await WaitForJobAsync(added.Data.JobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        return added.Data;
    }

    private async Task<ChromeItemDto> GetItemAsync(Setup setup, int storeAppId)
    {
        var (http, item) = await setup.Owner.Client.GetAsync<ChromeItemDto>($"chrome/item?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        return item.Data;
    }

    private async Task SyncAsync(Setup setup, int storeAppId)
    {
        var (http, job) = await setup.Owner.Client.PostAsync<long>("chrome/sync", new { organizationId = setup.OrganizationId, storeAppId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private async Task<(HttpResponseMessage Http, Response<ChangePlanDto> Plan)> PlanAsync(Setup setup, int storeAppId, object body)
    {
        var json = JsonSerializer.SerializeToNode(body).AsObject();

        json["organizationId"] = setup.OrganizationId;
        json["storeAppId"] = storeAppId;

        return await setup.Owner.Client.PostAsync<ChangePlanDto>("chrome/change", json);
    }

    private async Task<StoreJob> ApproveAsync(Setup setup, ChangePlanDto plan)
    {
        var (http, approved) = await setup.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = setup.OrganizationId, planId = plan.PlanId, confirm = plan.ConfirmText });

        http.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", approved.Messages?.Select(m => m.Message) ?? []));
        approved.Data.JobId.Should().NotBeNull();

        return await WaitForJobAsync(approved.Data.JobId.Value);
    }

    private async Task SetUploadRuleAsync(Setup setup, int appId)
    {
        var (http, body) = await setup.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = setup.OrganizationId,
            appId,
            rules = new[] { new { platform = ArtifactPlatform.Extension, enabled = true, target = DeliveryTarget.None, targetId = (string)null, targetName = (string)null, onWarning = DeliveryWarningAction.Deliver } },
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
    }

    [Fact]
    public async Task Connection_ShouldVerifyTheCredentialAgainstAnItem_AndKeepReplacementsOnTheSamePublisher()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var other = Chrome.AddItem();

        // Act
        var (_, verified) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });
        var (moveHttp, _) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(other.PublisherId, other.RefreshToken) });
        var (_, wrong) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Chrome, name = "wrong", secret = Secret(setup.PublisherId, "refresh-unknown", setup.ItemId) });
        var (_, rejected) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = wrong.Data });
        var (_, foreign) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Chrome, name = "foreign", secret = Secret(setup.PublisherId, other.RefreshToken, setup.ItemId) });
        var (_, foreignVerified) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = foreign.Data });
        var (invalidHttp, _) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Chrome, name = "bad", secret = JsonSerializer.Serialize(new { publisherId = setup.PublisherId }) });

        // Assert
        verified.Data.Status.Should().Be(ConnectionStatus.Verified, verified.Data.LastVerifyError);
        verified.Data.Fingerprint.PublisherId.Should().Be(setup.PublisherId);
        verified.Data.Fingerprint.TokenKind.Should().Be("OAuth client");
        JsonSerializer.Serialize(verified.Data).Should().NotContain(setup.RefreshToken).And.NotContain("client-secret-value");
        moveHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        rejected.Data.Status.Should().Be(ConnectionStatus.Rejected);
        foreignVerified.Data.Status.Should().Be(ConnectionStatus.Rejected);
        foreignVerified.Data.LastVerifyError.Should().Contain(setup.ItemId);
        invalidHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Item_ShouldSyncStatusRatingAndUsers_AndStayInsideItsOrganization()
    {
        // Arrange
        var setup = await NewSetupAsync(deployPercentage: 50);
        var fake = Chrome.Items[setup.ItemId];

        fake.RatingSummary = new JsonObject { ["rating"] = 4.5, ["ratingCount"] = "120", ["nonEmptyReviewCount"] = "40" };
        fake.UsersLast7Days = 25000;
        fake.Warned = true;

        var stranger = await NewSetupAsync();

        // Act
        var added = await AddItemAsync(setup);
        var item = await GetItemAsync(setup, added.StoreAppId);
        var (strangerHttp, _) = await stranger.Owner.Client.GetAsync<ChromeItemDto>($"chrome/item?organizationId={stranger.OrganizationId}&storeAppId={added.StoreAppId}");
        var (duplicateHttp, _) = await setup.Owner.Client.PostAsync<ChromeItemAddedDto>("chrome/item", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, itemId = setup.ItemId });
        var (badIdHttp, _) = await setup.Owner.Client.PostAsync<ChromeItemAddedDto>("chrome/item", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, itemId = "not-an-extension-id" });

        // Assert
        item.ItemId.Should().Be(setup.ItemId);
        item.PublisherId.Should().Be(setup.PublisherId);
        item.AppName.Should().Be("Kabuk Tabs");
        item.PublishedState.Should().Be(ChromeItemState.Published);
        item.PublishedVersion.Should().Be("1.0.0");
        item.PublishedDeployPercentage.Should().Be(50);
        item.SubmittedState.Should().Be(ChromeItemState.None);
        item.Warned.Should().BeTrue();
        item.Rating.Should().Be(4.5);
        item.RatingCount.Should().Be(120);
        item.WeeklyUsers.Should().Be(25000);
        item.StoreUrl.Should().EndWith(setup.ItemId);
        item.DashboardUrl.Should().Contain(setup.PublisherId);

        var builds = await _factory.DbAsync(db => db.Builds.Where(b => b.StoreAppId == added.StoreAppId).Select(b => b.Version).ToListAsync());

        builds.Should().Equal("1.0.0");
        strangerHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        duplicateHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        badIdHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExtensionZip_ShouldBeCheckedAndUploaded_ThenAnApprovedPlanSubmitsIt_AndTheReviewIsFollowed()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddItemAsync(setup);

        await SetUploadRuleAsync(setup, added.AppId);

        var client = TokenClient(await PatAsync(setup.Owner, setup.OrganizationId));

        // Act: intake checks and uploads the package (nothing is submitted).
        var (sessionId, _, _) = await UploadAsync(client, added.AppId, "kabuk-tabs-1.1.0.zip", FakeChromeWebStore.Package("1.1.0"));
        var session = await WaitForSessionAsync(sessionId);
        var publishesBeforePlan = Chrome.Items[setup.ItemId].PublishBodies.Count;

        // Plan a submission of the uploaded package, dry run first, then approve.
        var (planHttp, plan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish, deployPercentage = 100 });
        var publishesAfterDryRun = Chrome.Items[setup.ItemId].PublishBodies.Count;
        var job = await ApproveAsync(setup, plan.Data);
        var submitted = await GetItemAsync(setup, added.StoreAppId);

        // The review approves; the sync sees the publication.
        Chrome.Approve(setup.ItemId);
        await SyncAsync(setup, added.StoreAppId);
        var published = await GetItemAsync(setup, added.StoreAppId);

        // Assert
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        session.Package.ManifestVersion.Should().Be(3);
        session.Package.VersionName.Should().Be("1.1.0");
        publishesBeforePlan.Should().Be(0, "intake uploads only");

        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", plan.Messages?.Select(m => m.Message) ?? []));
        plan.Data.ConfirmText.Should().Be(setup.ItemId);
        plan.Data.Calls.Should().Contain(c => c.Path.EndsWith(":publish", StringComparison.Ordinal));
        publishesAfterDryRun.Should().Be(0, "a dry run sends nothing");

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        var body = Chrome.Items[setup.ItemId].PublishBodies.Single();
        body["publishType"]!.GetValue<string>().Should().Be("DEFAULT_PUBLISH");
        body["deployInfos"]![0]!["deployPercentage"]!.GetValue<int>().Should().Be(100);
        submitted.SubmittedState.Should().Be(ChromeItemState.PendingReview);
        submitted.SubmittedVersion.Should().Be("1.1.0");

        published.PublishedVersion.Should().Be("1.1.0");
        published.SubmittedState.Should().Be(ChromeItemState.None);
        published.Packages.Should().ContainSingle(p => p.SessionId == sessionId && p.Uploaded);

        var actions = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Store == StoreKind.Chrome).Select(a => a.Action).ToListAsync());

        actions.Should().Contain([AuditAction.ChromeItemAdded, AuditAction.ChromeSubmitted, AuditAction.ChromePublished]);
    }

    [Fact]
    public async Task PlanWithPackage_ShouldUploadThenSubmit_AndRefuseManifestV2OrALowerVersion()
    {
        // Arrange
        var setup = await NewSetupAsync(publishedVersion: "2.0.0");
        var added = await AddItemAsync(setup);
        var client = TokenClient(await PatAsync(setup.Owner, setup.OrganizationId));

        // No delivery rule: intake checks the packages and holds them for a member.
        var (goodId, _, _) = await UploadAsync(client, added.AppId, "tabs-2.1.zip", FakeChromeWebStore.Package("2.1"));
        var (mv2Id, _, _) = await UploadAsync(client, added.AppId, "tabs-mv2.zip", FakeChromeWebStore.Package("2.2", manifestVersion: 2));
        var (lowId, _, _) = await UploadAsync(client, added.AppId, "tabs-1.9.zip", FakeChromeWebStore.Package("1.9"));

        var good = await WaitForSessionAsync(goodId);
        var mv2 = await WaitForSessionAsync(mv2Id);
        var low = await WaitForSessionAsync(lowId);

        // Act
        var (mv2Http, mv2Plan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish, sessionId = mv2Id });
        var (lowHttp, lowPlan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish, sessionId = lowId });
        var (planHttp, plan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish, sessionId = goodId, staged = true });
        var job = await ApproveAsync(setup, plan.Data);
        var item = await GetItemAsync(setup, added.StoreAppId);

        // Assert
        good.Status.Should().Be(ArtifactSessionStatus.Held, good.StatusDescription);
        mv2.Status.Should().Be(ArtifactSessionStatus.Rejected);
        mv2.StatusDescription.Should().Contain("Manifest V3");
        low.Status.Should().Be(ArtifactSessionStatus.Rejected);

        mv2Http.StatusCode.Should().NotBe(HttpStatusCode.OK);
        mv2Plan.Messages.Should().Contain(m => m.Key == "ChromeManifestVersion");
        lowHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        lowPlan.Messages.Should().Contain(m => m.Key == "ChromeVersionNotHigher");

        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", plan.Messages?.Select(m => m.Message) ?? []));
        plan.Data.ConfirmText.Should().Be($"{setup.ItemId} 2.1");
        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        Chrome.Calls.Should().Contain(c => c.StartsWith("POST /upload/v2/publishers/", StringComparison.Ordinal) && c.EndsWith($"{setup.ItemId}:upload", StringComparison.Ordinal));
        Chrome.Items[setup.ItemId].PublishBodies.Single()["publishType"]!.GetValue<string>().Should().Be("STAGED_PUBLISH");
        item.SubmittedVersion.Should().Be("2.1");
        item.UploadedVersion.Should().Be("2.1");
    }

    [Fact]
    public async Task Rollout_Cancel_StagedPublish_AndRollback_ShouldEachBeOneApprovedWrite()
    {
        // Arrange
        var setup = await NewSetupAsync(deployPercentage: 20);
        var added = await AddItemAsync(setup);
        var fake = Chrome.Items[setup.ItemId];

        // Act: rollout must go up.
        var (lowerHttp, lower) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.DeployPercentage, deployPercentage = 10 });
        var (_, raise) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.DeployPercentage, deployPercentage = 60 });
        var raised = await ApproveAsync(setup, raise.Data);

        // A pending submission is cancelled.
        fake.UploadedVersion = "1.0.1";
        fake.Submitted = new FakeChromeWebStore.Revision { State = "PENDING_REVIEW", Version = "1.0.1" };
        await SyncAsync(setup, added.StoreAppId);
        var (blockedHttp, blocked) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish });
        var (_, cancel) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.CancelSubmission });
        var cancelled = await ApproveAsync(setup, cancel.Data);
        var afterCancel = await GetItemAsync(setup, added.StoreAppId);

        // A staged revision is published.
        fake.Submitted = new FakeChromeWebStore.Revision { State = "STAGED", Version = "1.0.2" };
        fake.UploadedVersion = "1.0.2";
        await SyncAsync(setup, added.StoreAppId);
        var (_, stagedPlan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.PublishStaged });
        var stagedJob = await ApproveAsync(setup, stagedPlan.Data);

        // The new version is rolled back to 1.0.0 as 1.0.3.
        var (noReasonHttp, _) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Rollback, targetVersion = "1.0.0", newVersion = "1.0.3" });
        var (_, rollback) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Rollback, targetVersion = "1.0.0", newVersion = "1.0.3", reason = "Crash on start" });
        var rolledBack = await ApproveAsync(setup, rollback.Data);
        var final = await GetItemAsync(setup, added.StoreAppId);

        // Assert
        lowerHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        lower.Messages.Should().Contain(m => m.Key == "ChromePercentageNotHigher");
        raised.Status.Should().Be(StoreJobStatus.Succeeded, raised.Error);
        fake.Published.Version.Should().Be("1.0.3");

        blockedHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        blocked.Messages.Should().Contain(m => m.Key == "ChromeSubmissionPending");
        cancelled.Status.Should().Be(StoreJobStatus.Succeeded, cancelled.Error);
        afterCancel.SubmittedState.Should().Be(ChromeItemState.Cancelled);

        stagedJob.Status.Should().Be(StoreJobStatus.Succeeded, stagedJob.Error);
        noReasonHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        rolledBack.Status.Should().Be(StoreJobStatus.Succeeded, rolledBack.Error);
        final.PublishedVersion.Should().Be("1.0.3");
        final.StatusHistory.Should().Contain(h => h.Revision == "published" && h.Version == "1.0.2");

        var actions = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Store == StoreKind.Chrome).Select(a => a.Action).ToListAsync());

        actions.Should().Contain([AuditAction.ChromeDeployPercentageChanged, AuditAction.ChromeSubmissionCancelled, AuditAction.ChromePublished, AuditAction.ChromeRolledBack]);
    }

    [Fact]
    public async Task Rejection_ShouldBeAuditedAndNotified()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddItemAsync(setup);
        var fake = Chrome.Items[setup.ItemId];

        fake.UploadedVersion = "1.0.5";
        var (_, plan) = await PlanAsync(setup, added.StoreAppId, new { kind = ChromeChangeKind.Publish });
        (await ApproveAsync(setup, plan.Data)).Status.Should().Be(StoreJobStatus.Succeeded);

        // Act
        Chrome.Reject(setup.ItemId);
        await SyncAsync(setup, added.StoreAppId);
        var item = await GetItemAsync(setup, added.StoreAppId);

        // Assert
        item.SubmittedState.Should().Be(ChromeItemState.Rejected);

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Action == AuditAction.ChromeRejected).Select(a => a.Summary).ToListAsync());

        audit.Should().ContainSingle().Which.Should().Contain("PendingReview -> Rejected");
    }

    [Fact]
    public async Task Reviews_And_Metrics_ShouldBeReadThroughTheSharedSyncs()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddItemAsync(setup);
        var fake = Chrome.Items[setup.ItemId];
        var day = DateTime.UtcNow.Date.AddDays(-3);

        fake.Reviews.Add(new JsonObject { ["name"] = "reviews/r-1", ["rating"] = 2, ["comment"] = "Crashes on start", ["createTime"] = "2026-11-20T10:00:00Z", ["authorInfo"] = new JsonObject { ["displayName"] = "Ayse" } });
        fake.Reviews.Add(new JsonObject { ["name"] = "reviews/r-2", ["rating"] = 5, ["comment"] = "Great", ["createTime"] = "2026-11-21T10:00:00Z", ["authorInfo"] = new JsonObject { ["displayName"] = "Ali" } });
        fake.MetricRows.Add(new JsonObject
        {
            ["date"] = new JsonObject { ["year"] = day.Year, ["month"] = day.Month, ["day"] = day.Day },
            ["dimensionValues"] = new JsonArray("TR"),
            ["metricValues"] = new JsonArray(new JsonObject { ["metric"] = "installs", ["value"] = "7" }, new JsonObject { ["metric"] = "impressions", ["value"] = "300" }, new JsonObject { ["metric"] = "page-views", ["value"] = "40" }),
        });

        // Act
        var (reviewHttp, reviewJobs) = await setup.Owner.Client.PostAsync<List<long>>("reviews/sync", new { organizationId = setup.OrganizationId });
        var (metricHttp, metricJobs) = await setup.Owner.Client.PostAsync<List<long>>("store-metrics/sync", new { organizationId = setup.OrganizationId, appId = added.AppId });

        foreach (var jobId in reviewJobs.Data.Concat(metricJobs.Data))
            (await WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        // Assert
        reviewHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        metricHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        var reviews = await _factory.DbAsync(db => db.StoreReviews.Where(r => r.StoreAppId == added.StoreAppId).OrderBy(r => r.ExternalReviewId).ToListAsync());

        reviews.Select(r => (r.ExternalReviewId, r.Rating, r.AuthorName)).Should().Equal(("r-1", 2, "Ayse"), ("r-2", 5, "Ali"));

        var metrics = await _factory.DbAsync(db => db.StoreMetrics.Where(m => m.StoreAppId == added.StoreAppId).ToListAsync());

        metrics.Should().Contain(m => m.Country == "TR" && m.Downloads == 7 && m.PageViews == 40);
        metrics.Should().Contain(m => m.Country == StoreMetricRules.AllCountries && m.Downloads == 7 && m.Impressions == 300, "impressions have no country dimension, they count for every country");

        var (replyHttp, _) = await setup.Owner.Client.PostAsync<long>("reviews/reply", new { organizationId = setup.OrganizationId, reviewId = reviews[0].Id, body = "Thanks" });

        replyHttp.StatusCode.Should().NotBe(HttpStatusCode.OK, "the Chrome Web Store API has no replies");
    }

    [Fact]
    public async Task DeleteApp_ShouldRemoveTheItemWithItsRecordAndLetItBeAddedAgain()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var added = await AddItemAsync(setup);
        var stranger = await NewSetupAsync();

        // Act
        var (foreign, _) = await stranger.Owner.Client.SendAsync<int>(HttpMethod.Delete, "apps/app", new { organizationId = stranger.OrganizationId, appId = added.AppId });
        var (badId, _) = await setup.Owner.Client.SendAsync<int>(HttpMethod.Delete, "apps/app", new { organizationId = setup.OrganizationId, appId = 0 });
        var (deleteHttp, deleted) = await setup.Owner.Client.SendAsync<int>(HttpMethod.Delete, "apps/app", new { organizationId = setup.OrganizationId, appId = added.AppId });
        var (againHttp, _) = await setup.Owner.Client.SendAsync<int>(HttpMethod.Delete, "apps/app", new { organizationId = setup.OrganizationId, appId = added.AppId });
        var records = await _factory.DbAsync(db => db.StoreApps.AsNoTracking().CountAsync(s => s.Id == added.StoreAppId));
        var items = await _factory.DbAsync(db => db.ChromeItems.AsNoTracking().CountAsync(i => i.StoreAppId == added.StoreAppId));
        var (itemHttp, _) = await setup.Owner.Client.GetAsync<ChromeItemDto>($"chrome/item?organizationId={setup.OrganizationId}&storeAppId={added.StoreAppId}");
        var audited = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == setup.OrganizationId && a.Action == AuditAction.AppDeleted));
        var readded = await AddItemAsync(setup);

        // Assert
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        deleteHttp.StatusCode.Should().Be(HttpStatusCode.OK, string.Join(", ", deleted.Messages?.Select(m => m.Message) ?? []));
        deleted.Data.Should().Be(added.AppId);
        againHttp.StatusCode.Should().Be(HttpStatusCode.NotFound, "the app is gone");
        records.Should().Be(0);
        items.Should().Be(0, "the item row goes with its record");
        itemHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        audited.Should().Be(1);
        readded.StoreAppId.Should().NotBe(added.StoreAppId, "the same extension id can be added again after a delete");
    }
}
