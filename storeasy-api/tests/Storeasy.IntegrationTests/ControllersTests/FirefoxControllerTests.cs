using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Firefox Add-ons: connection, account add-ons, listing, previews, icon, version uploads through intake, review state, ratings and replies; against a fake AMO.")]
public class FirefoxControllerTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private FakeAmo Amo => _factory.Amo;

    private sealed record Setup(TestUser Owner, int OrganizationId, int ConnectionId, FakeAmo.Account Account, FakeAmo.Addon Addon);

    private static string Secret(FakeAmo.Account account, string secret = null) => JsonSerializer.Serialize(new { issuer = account.Issuer, secret = secret ?? account.Secret });

    private async Task<Setup> NewSetupAsync(string version = "1.0.0")
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var account = Amo.AddAccount();
        var addon = Amo.AddAddon(account, version);
        var (http, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new
        {
            organizationId = organization.Data,
            store = StoreKind.Firefox,
            name = "AMO",
            secret = Secret(account),
            icon = StoreConnectionIcons.FirefoxAddons,
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK, connection.MessagesToString());

        return new Setup(owner, organization.Data, connection.Data, account, addon);
    }

    /// <summary>
    /// Runs the connection's sync, which adds the account's add-ons; returns the store record of <see cref="Setup.Addon"/>.
    /// </summary>
    private async Task<(int AppId, int StoreAppId)> SyncAccountAsync(Setup setup)
    {
        var (http, jobs) = await setup.Owner.Client.PostAsync<List<long>>("apps/sync", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });

        http.StatusCode.Should().Be(HttpStatusCode.OK, jobs.MessagesToString());

        foreach (var jobId in jobs.Data)
        {
            var job = await WaitForJobAsync(jobId);

            job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        }

        var externalId = setup.Addon.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var record = await _factory.DbAsync(db => db.StoreApps.Where(s => s.OrganizationId == setup.OrganizationId && s.Store == StoreKind.Firefox && s.ExternalId == externalId)
                                                              .Select(s => new { s.AppId, s.Id })
                                                              .SingleAsync());

        return (record.AppId, record.Id);
    }

    private async Task<FirefoxAddonDto> GetAddonAsync(Setup setup, int storeAppId)
    {
        var (http, addon) = await setup.Owner.Client.GetAsync<FirefoxAddonDto>($"firefox/addon?organizationId={setup.OrganizationId}&storeAppId={storeAppId}");

        http.StatusCode.Should().Be(HttpStatusCode.OK, addon.MessagesToString());

        return addon.Data;
    }

    private Task<(HttpResponseMessage Http, Response<ChangePlanDto> Body)> PlanAsync(Setup setup, int storeAppId, object change)
    {
        var body = JsonSerializer.SerializeToNode(change, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();

        body["organizationId"] = setup.OrganizationId;
        body["storeAppId"] = storeAppId;

        return setup.Owner.Client.PostAsync<ChangePlanDto>("firefox/change", body);
    }

    private async Task<StoreJob> ApproveAsync(Setup setup, ChangePlanDto plan)
    {
        var (http, approved) = await setup.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = setup.OrganizationId, planId = plan.PlanId, confirm = plan.ConfirmText });

        http.StatusCode.Should().Be(HttpStatusCode.OK, approved.MessagesToString());
        approved.Data.JobId.Should().NotBeNull();

        return await WaitForJobAsync(approved.Data.JobId.Value);
    }

    private async Task SyncAsync(Setup setup, int storeAppId)
    {
        var (http, job) = await setup.Owner.Client.PostAsync<long>("firefox/sync", new { organizationId = setup.OrganizationId, storeAppId });

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        (await WaitForJobAsync(job.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
    }

    private Task<List<AuditAction>> AuditAsync(Setup setup)
        => _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Store == StoreKind.Firefox).Select(a => a.Action).ToListAsync());

    [Fact]
    public async Task Connection_ShouldVerifyTheApiKey_AndKeepReplacementsOnTheSameAccount()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var other = Amo.AddAccount();
        var secondKey = new FakeAmo.Account($"user:{setup.Account.UserId}:99", setup.Account.Secret + "00", setup.Account.UserId, setup.Account.Username);

        Amo.Accounts[secondKey.Issuer] = secondKey;

        // Act
        var (_, verified) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });
        var (moveHttp, _) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(other) });
        var (replaceHttp, replaced) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = Secret(secondKey) });
        var (_, wrong) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Firefox, name = "wrong", secret = Secret(setup.Account, new string('f', 64)) });
        var (_, rejected) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = wrong.Data });
        var (invalidHttp, _) = await setup.Owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = setup.OrganizationId, store = StoreKind.Firefox, name = "bad", secret = JsonSerializer.Serialize(new { issuer = "12345", secret = "x" }) });

        // Assert
        verified.Data.Status.Should().Be(ConnectionStatus.Verified, verified.Data.LastVerifyError);
        verified.Data.Fingerprint.KeyId.Should().Be(setup.Account.Issuer);
        verified.Data.Fingerprint.AccountLogin.Should().Be($"user {setup.Account.UserId}");
        JsonSerializer.Serialize(verified.Data).Should().NotContain(setup.Account.Secret);
        moveHttp.StatusCode.Should().Be(HttpStatusCode.Conflict);
        replaceHttp.StatusCode.Should().Be(HttpStatusCode.OK, replaced.MessagesToString());
        replaced.Data.Fingerprint.KeyId.Should().Be(secondKey.Issuer);
        rejected.Data.Status.Should().Be(ConnectionStatus.Rejected);
        invalidHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AccountSync_ShouldAddTheAddons_ReadTheirListingAndVersions_AndStayInsideTheOrganization()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var stranger = await NewSetupAsync();

        Amo.AddRating(setup.Addon, 4, "Nice", "Ali");

        // Act
        var (appId, storeAppId) = await SyncAccountAsync(setup);
        var addon = await GetAddonAsync(setup, storeAppId);
        var (strangerHttp, _) = await stranger.Owner.Client.GetAsync<FirefoxAddonDto>($"firefox/addon?organizationId={stranger.OrganizationId}&storeAppId={storeAppId}");
        var (duplicateHttp, _) = await setup.Owner.Client.PostAsync<FirefoxAddonAddedDto>("firefox/addon", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, addon = setup.Addon.Guid });
        var (badHttp, _) = await setup.Owner.Client.PostAsync<FirefoxAddonAddedDto>("firefox/addon", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, addon = "not an id!" });
        await SyncAccountAsync(setup);

        // Assert
        addon.AppName.Should().Be("Kabuk Tabs");
        addon.Guid.Should().Be(setup.Addon.Guid);
        addon.Status.Should().Be(FirefoxAddonStatus.Public);
        addon.ListedVersion.Should().Be("1.0.0");
        addon.AverageDailyUsers.Should().Be(1200);
        addon.WeeklyDownloads.Should().Be(300);
        addon.Rating.Should().Be(4);
        addon.RatingCount.Should().Be(1);
        addon.Listing.Should().Contain(l => l.Field == "Name" && l.Locale == "tr" && l.Value == "Kabuk Tabs TR");
        addon.Listing.Should().Contain(l => l.Field == "Homepage" && l.Value == "https://milvasoft.com/kabuk");
        addon.Versions.Should().ContainSingle(v => v.Version == "1.0.0" && v.FileStatus == FirefoxFileStatus.Public && v.Channel == FirefoxVersionChannel.Listed);
        addon.StoreUrl.Should().Contain(setup.Addon.Slug);

        var records = await _factory.DbAsync(db => db.StoreApps.CountAsync(s => s.OrganizationId == setup.OrganizationId && s.Store == StoreKind.Firefox));
        var builds = await _factory.DbAsync(db => db.Builds.Where(b => b.StoreAppId == storeAppId).Select(b => b.Version).ToListAsync());

        records.Should().Be(1, "a second sync adds nothing");
        builds.Should().Equal("1.0.0");
        appId.Should().BePositive();
        strangerHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        duplicateHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        badHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AuditAsync(setup)).Should().Contain(AuditAction.FirefoxAddonAdded);
    }

    [Fact]
    public async Task ListingPlan_ShouldBeADryRunFirst_ThenPatchTheTextsPerLocale()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var (_, storeAppId) = await SyncAccountAsync(setup);

        // Act
        var (planHttp, plan) = await PlanAsync(setup, storeAppId, new
        {
            kind = FirefoxChangeKind.Listing,
            listing = new[]
            {
                new { field = "Summary", locale = "en-US", value = "Tabs that stay tidy, fast." },
                new { field = "Name", locale = "de", value = "Kabuk Tabs DE" },
                new { field = "Name", locale = "tr", value = "" },
            },
            categories = new[] { "tabs", "privacy-security" },
        });
        var patchesAfterDryRun = setup.Addon.Patches.Count;
        var (badHttp, bad) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Listing, listing = new[] { new { field = "Name", locale = "en-US", value = "" } } });
        var job = await ApproveAsync(setup, plan.Data);
        var addon = await GetAddonAsync(setup, storeAppId);

        // Assert
        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, plan.MessagesToString());
        plan.Data.ConfirmText.Should().Be(setup.Addon.Guid);
        patchesAfterDryRun.Should().Be(0, "a dry run sends nothing");
        badHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        bad.Messages.Should().Contain(m => m.Key == "FirefoxDefaultLocaleRequired");

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        var patch = setup.Addon.Patches.Single();
        patch["summary"]!["en-US"]!.GetValue<string>().Should().Be("Tabs that stay tidy, fast.");
        patch["name"]!["tr"].Should().BeNull();
        patch["categories"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("privacy-security", "tabs");
        addon.Listing.Should().Contain(l => l.Field == "Name" && l.Locale == "de" && l.Value == "Kabuk Tabs DE");
        addon.Listing.Should().NotContain(l => l.Field == "Name" && l.Locale == "tr");
        (await AuditAsync(setup)).Should().Contain(AuditAction.FirefoxListingUpdated);
    }

    [Fact]
    public async Task Xpi_ShouldBeCheckedByIntake_ThenAnApprovedPlanUploadsAndSubmitsIt_AndTheReviewIsFollowed()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var (appId, storeAppId) = await SyncAccountAsync(setup);
        var client = TokenClient(await PatAsync(setup.Owner, setup.OrganizationId));
        var source = await AssetAsync(new Org(setup.Owner, setup.OrganizationId, 0, 0, TestApi.Unique()), [1, 2, 3], "kabuk-src.zip", "application/zip", AssetPurpose.BuildArtifact);

        // No delivery rule: intake checks the packages and holds them for a member.
        var (goodId, _, _) = await UploadAsync(client, appId, "kabuk-1.1.0.xpi", FakeAmo.Xpi("1.1.0", setup.Addon.Guid, minified: true));
        var (lowId, _, _) = await UploadAsync(client, appId, "kabuk-0.9.xpi", FakeAmo.Xpi("0.9", setup.Addon.Guid));
        var (foreignId, _, _) = await UploadAsync(client, appId, "other-2.0.xpi", FakeAmo.Xpi("2.0", "other@milvasoft.com"));
        var good = await WaitForSessionAsync(goodId);
        var low = await WaitForSessionAsync(lowId);
        var foreign = await WaitForSessionAsync(foreignId);

        // Act
        var (lowPlanHttp, lowPlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Version, sessionId = lowId, channel = FirefoxVersionChannel.Listed });
        var (planHttp, plan) = await PlanAsync(setup, storeAppId, new
        {
            kind = FirefoxChangeKind.Version,
            sessionId = goodId,
            channel = FirefoxVersionChannel.Listed,
            releaseNotes = new[] { new { locale = "en-US", value = "Faster tabs." } },
            approvalNotes = "npm ci && npm run build",
            sourceAssetId = source,
        });
        var uploadsAfterDryRun = Amo.Uploads.Values.Count(u => u.UserId == setup.Account.UserId);
        var job = await ApproveAsync(setup, plan.Data);
        var submitted = await GetAddonAsync(setup, storeAppId);

        // The review approves; the sync sees it.
        Amo.Approve(setup.Addon.Id, "1.1.0");
        await SyncAsync(setup, storeAppId);
        var approved = await GetAddonAsync(setup, storeAppId);

        // Assert
        good.Status.Should().Be(ArtifactSessionStatus.Held, good.StatusDescription);
        good.Package.Kind.Should().Be(PackageKind.Xpi);
        good.Package.Identifier.Should().Be(setup.Addon.Guid);
        good.Checks.Should().Contain(c => c.Title == "FirefoxSourceRequired" && c.Status == ReadinessStatus.Warning);
        low.Status.Should().Be(ArtifactSessionStatus.Rejected, "0.9 is below the add-on's 1.0.0");
        foreign.Status.Should().Be(ArtifactSessionStatus.Rejected, "the gecko id is another add-on's");

        lowPlanHttp.StatusCode.Should().Be(HttpStatusCode.Conflict, lowPlan.MessagesToString());
        lowPlan.Messages.Should().Contain(m => m.Key == "FirefoxVersionNotHigher");

        planHttp.StatusCode.Should().Be(HttpStatusCode.OK, plan.MessagesToString());
        plan.Data.ConfirmText.Should().Be($"{setup.Addon.Guid} 1.1.0");
        uploadsAfterDryRun.Should().Be(0, "a dry run sends nothing");

        job.Status.Should().Be(StoreJobStatus.Succeeded, job.Error);
        var version = setup.Addon.Versions.Single(v => v.Number == "1.1.0");
        version.Channel.Should().Be("listed");
        version.ReleaseNotes["en-US"].Should().Be("Faster tabs.");
        version.ApprovalNotes.Should().Be("npm ci && npm run build");
        version.SourceFileName.Should().Be("kabuk-src.zip");
        submitted.LatestVersion.Should().Be("1.1.0");
        submitted.LatestFileStatus.Should().Be(FirefoxFileStatus.Unreviewed);
        submitted.Packages.Should().Contain(p => p.SessionId == goodId && p.Version == "1.1.0" && !p.Blocked);

        approved.LatestFileStatus.Should().Be(FirefoxFileStatus.Public);
        approved.ListedVersion.Should().Be("1.1.0");
        approved.StatusHistory.Should().Contain(h => h.Version == "1.1.0" && h.State == "Public");

        (await AuditAsync(setup)).Should().Contain([AuditAction.FirefoxVersionSubmitted, AuditAction.FirefoxApproved]);
    }

    [Fact]
    public async Task UnlistedVersion_ShouldBeSignedAtOnce_AndRejectionsShouldBeAudited()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var (appId, storeAppId) = await SyncAccountAsync(setup);
        var client = TokenClient(await PatAsync(setup.Owner, setup.OrganizationId));
        var (unlistedId, _, _) = await UploadAsync(client, appId, "kabuk-1.0.1.xpi", FakeAmo.Xpi("1.0.1", setup.Addon.Guid));
        var (listedId, _, _) = await UploadAsync(client, appId, "kabuk-1.0.2.xpi", FakeAmo.Xpi("1.0.2", setup.Addon.Guid));

        await WaitForSessionAsync(unlistedId);
        await WaitForSessionAsync(listedId);

        // Act
        var (_, unlistedPlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Version, sessionId = unlistedId, channel = FirefoxVersionChannel.Unlisted });
        var unlistedJob = await ApproveAsync(setup, unlistedPlan.Data);
        var (_, listedPlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Version, sessionId = listedId, channel = FirefoxVersionChannel.Listed });
        var listedJob = await ApproveAsync(setup, listedPlan.Data);

        Amo.Reject(setup.Addon.Id, "1.0.2");
        await SyncAsync(setup, storeAppId);
        var addon = await GetAddonAsync(setup, storeAppId);

        // Assert
        unlistedJob.Status.Should().Be(StoreJobStatus.Succeeded, unlistedJob.Error);
        listedJob.Status.Should().Be(StoreJobStatus.Succeeded, listedJob.Error);
        addon.Versions.Should().Contain(v => v.Version == "1.0.1" && v.Channel == FirefoxVersionChannel.Unlisted && v.FileStatus == FirefoxFileStatus.Public);
        addon.LatestFileStatus.Should().Be(FirefoxFileStatus.Disabled);

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == setup.OrganizationId && a.Action == AuditAction.FirefoxRejected).Select(a => a.Summary).ToListAsync());

        audit.Should().ContainSingle().Which.Should().Contain("Unreviewed -> Disabled");
    }

    [Fact]
    public async Task PreviewsAndIcon_ShouldBeUploadedFromAssets_AfterApproval()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var (_, storeAppId) = await SyncAccountAsync(setup);
        var org = new Org(setup.Owner, setup.OrganizationId, 0, 0, TestApi.Unique());
        var shot = await AssetAsync(org, Png(1280, 800), "shot-1.png", "image/png");
        var icon = await AssetAsync(org, Png(128, 128, salt: 1), "icon.png", "image/png");
        var wide = await AssetAsync(org, Png(200, 100, salt: 2), "wide.png", "image/png");

        // Act
        var (previewHttp, previewPlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Previews, addPreviewAssetIds = new[] { shot } });
        var previewJob = await ApproveAsync(setup, previewPlan.Data);
        var withPreview = await GetAddonAsync(setup, storeAppId);
        var (_, deletePlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Previews, deletePreviewIds = new[] { withPreview.Previews.Single().Id } });
        var deleteJob = await ApproveAsync(setup, deletePlan.Data);
        var (iconHttp, iconPlan) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Icon, iconAssetId = icon });
        var iconJob = await ApproveAsync(setup, iconPlan.Data);
        var (wideHttp, wideBody) = await PlanAsync(setup, storeAppId, new { kind = FirefoxChangeKind.Icon, iconAssetId = wide });
        var after = await GetAddonAsync(setup, storeAppId);

        // Assert
        previewHttp.StatusCode.Should().Be(HttpStatusCode.OK, previewPlan.MessagesToString());
        previewJob.Status.Should().Be(StoreJobStatus.Succeeded, previewJob.Error);
        withPreview.Previews.Should().ContainSingle(p => p.ImageUrl.EndsWith("shot-1.png", StringComparison.Ordinal));
        deleteJob.Status.Should().Be(StoreJobStatus.Succeeded, deleteJob.Error);
        iconHttp.StatusCode.Should().Be(HttpStatusCode.OK, iconPlan.MessagesToString());
        iconJob.Status.Should().Be(StoreJobStatus.Succeeded, iconJob.Error);
        after.Previews.Should().BeEmpty();
        after.IconUrl.Should().EndWith("icon.png");
        wideHttp.StatusCode.Should().NotBe(HttpStatusCode.OK);
        wideBody.Messages.Should().Contain(m => m.Key == "FirefoxIconNotSquare");
        (await AuditAsync(setup)).Should().Contain([AuditAction.FirefoxPreviewsChanged, AuditAction.FirefoxIconUpdated]);
    }

    [Fact]
    public async Task Ratings_ShouldBeReadThroughTheSharedReviewSync_AndAnsweredAsTheDeveloper()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var (_, storeAppId) = await SyncAccountAsync(setup);

        Amo.AddRating(setup.Addon, 2, "Crashes on start", "Ayse");
        Amo.AddRating(setup.Addon, 5, "Great", "Ali");

        // Act
        var (syncHttp, jobs) = await setup.Owner.Client.PostAsync<List<long>>("reviews/sync", new { organizationId = setup.OrganizationId });

        foreach (var jobId in jobs.Data)
            (await WaitForJobAsync(jobId)).Status.Should().Be(StoreJobStatus.Succeeded);

        var reviews = await _factory.DbAsync(db => db.StoreReviews.Where(r => r.StoreAppId == storeAppId).OrderBy(r => r.Rating).ToListAsync());
        var (replyHttp, replyJob) = await setup.Owner.Client.PostAsync<long>("reviews/reply", new { organizationId = setup.OrganizationId, reviewId = reviews[0].Id, body = "Fixed in 1.1, thanks." });

        (await WaitForJobAsync(replyJob.Data)).Status.Should().Be(StoreJobStatus.Succeeded);

        var answered = await _factory.DbAsync(db => db.StoreReviews.AsNoTracking().SingleAsync(r => r.Id == reviews[0].Id));

        // Assert
        syncHttp.StatusCode.Should().Be(HttpStatusCode.OK, jobs.MessagesToString());
        reviews.Select(r => (r.Rating, r.AuthorName, r.Body)).Should().Equal((2, "Ayse", "Crashes on start"), (5, "Ali", "Great"));
        replyHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        answered.ReplyState.Should().Be(ReviewReplyState.Published);
        answered.ReplyExternalId.Should().NotBeNullOrEmpty();
        setup.Addon.Ratings.Should().Contain(r => r["is_developer_reply"]!.GetValue<bool>() && r["body"]!.GetValue<string>() == "Fixed in 1.1, thanks.");
    }
}
