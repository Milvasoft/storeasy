using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "App Store content engine: plans, approval, apply jobs, readiness against the fake App Store Connect.")]
public class AppleContentTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private FakeAppStoreConnect Apple => _factory.Stores.Apple;

    [Fact]
    public async Task ListingPlan_ShouldDiffPerFieldAndApplyThroughTheJobWithAudit()
    {
        // Arrange
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        var versionId = Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        // Act: plan (dry run) changes the description of en-US and creates a de-DE localization.
        var (http, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[]
            {
                new { locale = "en-US", field = "description", value = "A better compass." },
                new { locale = "en-US", field = "keywords", value = "compass,map" },
                new { locale = "de-DE", field = "name", value = "Kompass" },
                new { locale = "de-DE", field = "description", value = "Ein Kompass." },
            },
        });

        var before = Apple.VersionLocalizations.Values.Single(l => (string)l["version"] == versionId && (string)l["locale"] == "en-US")["description"].GetValue<string>();
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        plan.Data.Status.Should().Be(ChangePlanStatus.Pending);
        plan.Data.Items.Should().HaveCount(3, "the unchanged keywords are not part of the diff");
        plan.Data.Items.Should().ContainSingle(i => i.Field == "description" && i.Locale == "en-US" && i.Before == "A compass app." && i.After == "A better compass." && i.Action == ChangeAction.Update);
        plan.Data.Warnings.Should().Contain(w => w.Locale == "de-DE");
        before.Should().Be("A compass app.", "a plan writes nothing");

        applied.Status.Should().Be(ChangePlanStatus.Applied);
        Apple.VersionLocalizations.Values.Single(l => (string)l["version"] == versionId && (string)l["locale"] == "en-US")["description"].GetValue<string>().Should().Be("A better compass.");
        Apple.InfoLocalizations.Values.Should().Contain(l => (string)l["locale"] == "de-DE" && (string)l["name"] == "Kompass");
        Apple.VersionLocalizations.Values.Where(l => (string)l["version"] == versionId && (string)l["locale"] == "de-DE").Should().ContainSingle("the version localization Apple created with the app info is patched, not duplicated")
             .Which["description"].GetValue<string>().Should().Be("Ein Kompass.");

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.ListingApplied).ToListAsync());
        audit.Should().HaveCount(3, "one audit entry per changed field");
        audit.Should().OnlyContain(a => a.UserName == org.Owner.UserName && a.Details.Contains("\"Field\""));
    }

    [Fact]
    public async Task ListingPlan_OverTheKeywordByteLimit_ShouldBeRefusedWithTheFieldKey()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        // 60 Turkish letters of two bytes each: 60 characters but 120 bytes.
        var keywords = new string('ş', 60);

        var (http, body) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[] { new { locale = "tr", field = "keywords", value = keywords } },
        });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Messages.Should().Contain(m => m.Message.Contains("keywords") && m.Message.Contains("120") && m.Message.Contains("100"));
    }

    [Fact]
    public async Task Approval_AfterTheStoreChanged_ShouldBeAConflictAndWriteNothing()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        var versionId = Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        var (_, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[] { new { locale = "en-US", field = "description", value = "Mine." } },
        });

        // Someone edits App Store Connect meanwhile.
        Apple.VersionLocalizations.Values.Single(l => (string)l["version"] == versionId)["description"] = "Theirs.";

        var (http, _) = await org.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = org.OrganizationId, planId = plan.Data.PlanId });

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        Apple.VersionLocalizations.Values.Single(l => (string)l["version"] == versionId)["description"].GetValue<string>().Should().Be("Theirs.");
    }

    [Fact]
    public async Task Plans_ShouldBeIsolatedByOrganizationAndRole()
    {
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var viewer = await MemberAsync(org, OrgRole.Viewer);

        var (_, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[] { new { locale = "en-US", field = "subtitle", value = "New subtitle" } },
        });

        // Act
        var (otherPlan, _) = await other.Owner.Client.GetAsync<ChangePlanDto>($"plans/plan?organizationId={other.OrganizationId}&planId={plan.Data.PlanId}");
        var (otherListing, _) = await other.Owner.Client.GetAsync<ListingDto>($"listings?organizationId={other.OrganizationId}&storeAppId={record.StoreAppId}");
        var (otherApprove, _) = await other.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = other.OrganizationId, planId = plan.Data.PlanId });
        var (viewerPlan, _) = await viewer.Client.PostAsync<ChangePlanDto>("listings/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, changes = new[] { new { locale = "en-US", field = "subtitle", value = "x" } } });
        var (viewerRead, _) = await viewer.Client.GetAsync<ChangePlanDto>($"plans/plan?organizationId={org.OrganizationId}&planId={plan.Data.PlanId}");
        var (_, queue) = await org.Owner.Client.PatchListAsync<ChangePlanListDto>("plans", new { organizationId = org.OrganizationId, pageNumber = 1, rowCount = 20 });
        var (discard, discarded) = await org.Owner.Client.PostAsync<ChangePlanDto>("plans/discard", new { organizationId = org.OrganizationId, planId = plan.Data.PlanId });

        // Assert
        otherPlan.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherListing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherApprove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        viewerPlan.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerRead.StatusCode.Should().Be(HttpStatusCode.OK);
        queue.Data.Should().ContainSingle(p => p.PlanId == plan.Data.PlanId && p.Status == ChangePlanStatus.Pending);
        discard.StatusCode.Should().Be(HttpStatusCode.OK);
        discarded.Data.Status.Should().Be(ChangePlanStatus.Discarded);
    }

    [Fact]
    public async Task ScreenshotPlan_ShouldCheckFilesAgainstTheCatalogAndUploadThroughReserveAndCommit()
    {
        // Arrange: the app supports iPad, the iPad 13" set is empty.
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId, profile: new AppProfile { DeviceFamilies = ["iphone", "ipad"] });

        var good = await AssetAsync(org, Png(2064, 2752), "ipad-1.png", "image/png");
        var wrongSize = await AssetAsync(org, Png(1320, 2868, salt: 1), "iphone.png", "image/png");
        var alpha = await AssetAsync(org, Png(2064, 2752, alpha: true, salt: 2), "alpha.png", "image/png");

        // Act
        var (_, rejected) = await org.Owner.Client.PostAsync<ChangePlanDto>("screenshots/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, locale = "en-US", classKey = "APP_IPAD_PRO_3GEN_129", assetIds = new[] { wrongSize, alpha } });
        var (http, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("screenshots/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, locale = "en-US", classKey = "APP_IPAD_PRO_3GEN_129", assetIds = new[] { good, wrongSize } });
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);
        var (_, sets) = await org.Owner.Client.GetAsync<MediaSetListDto>($"screenshots/sets?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}&locale=en-US");
        var (_, again) = await org.Owner.Client.PostAsync<ChangePlanDto>("screenshots/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, locale = "en-US", classKey = "APP_IPAD_PRO_3GEN_129", assetIds = new[] { good } });

        // Assert
        rejected.Data.Files.Should().OnlyContain(f => f.Result == "Rejected");
        rejected.Data.Files.Should().Contain(f => f.FileName == "iphone.png" && f.SuggestedClassKey == "APP_IPHONE_67");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        plan.Data.Files.Should().Contain(f => f.FileName == "ipad-1.png" && f.Result == "Accepted");
        plan.Data.Items.Should().ContainSingle(i => i.Action == ChangeAction.Create && i.After == "ipad-1.png");
        plan.Data.Calls.Should().Contain(c => c.Method == "POST" && c.Path == "/v1/appScreenshotSets");
        applied.Status.Should().Be(ChangePlanStatus.Applied);

        var shot = Apple.Screenshots.Values.Single(s => (string)s["fileName"] == "ipad-1.png");
        shot["state"].GetValue<string>().Should().Be("COMPLETE", "the uploaded bytes match the MD5 committed");

        var ipad = sets.Data.Classes.Single(c => c.ClassKey == "APP_IPAD_PRO_3GEN_129");
        ipad.Fill.Should().Be(MediaSlotFill.Own);
        ipad.Count.Should().Be(1);
        sets.Data.Classes.Single(c => c.ClassKey == "APP_IPHONE_61").Should().Match<MediaClassDto>(c => c.Required && c.Fill == MediaSlotFill.None);

        again.Messages.Should().Contain(m => m.Key == "Plan.NoChanges", "the same image is skipped by its MD5");
    }

    [Fact]
    public async Task SubmitForReview_ShouldNeedTheTypedConfirmationAndThePermission()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        var versionId = Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var editor = await MemberAsync(org, OrgRole.Editor);

        var (editorHttp, _) = await editor.Client.PostAsync<ChangePlanDto>("releases/submit-for-review", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, confirm = "Pusula 1.0" });
        var (wrongHttp, wrong) = await org.Owner.Client.PostAsync<ChangePlanDto>("releases/submit-for-review", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, confirm = "Pusula 2.0" });
        var (http, submitted) = await org.Owner.Client.PostAsync<ChangePlanDto>("releases/submit-for-review", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, confirm = "Pusula 1.0" });
        var job = await WaitForJobAsync(submitted.Data.JobId.Value);

        editorHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrong.Messages.Should().Contain(m => m.Key == "Plan.ConfirmMismatch");
        Apple.SubmittedVersions.Should().Contain(versionId);
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        submitted.Data.ConfirmText.Should().Be("Pusula 1.0");
        submitted.Data.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        job.Status.Should().Be(StoreJobStatus.Succeeded);
        Apple.Versions[versionId]["appVersionState"].GetValue<string>().Should().Be("WAITING_FOR_REVIEW");
    }

    [Fact]
    public async Task ReviewInfo_ShouldSealTheDemoPasswordAndSendItOnlyFromTheJob()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        var versionId = Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        var (_, plan) = await org.Owner.Client.PutAsync<ChangePlanDto>("review-info", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            demoAccountRequired = true,
            demoAccountName = "reviewer@example.test",
            demoPassword = "S3cret-demo-pass",
        });

        var stored = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().SingleAsync(p => p.Id == plan.Data.PlanId));
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);
        var (_, info) = await org.Owner.Client.GetAsync<ReviewInfoDto>($"review-info?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");
        var jobs = (await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().Where(j => j.OrganizationId == org.OrganizationId).Select(j => new { j.Payload, j.Result, j.Summary }).ToListAsync()))
                   .Select(j => $"{j.Payload}{j.Result}{j.Summary}")
                   .ToList();

        stored.Changes.Should().NotContain("S3cret-demo-pass");
        plan.Data.Items.Should().Contain(i => i.Field == "demoAccountPassword" && i.After == "********");
        Apple.ReviewDetails[versionId]["demoAccountPassword"].GetValue<string>().Should().Be("S3cret-demo-pass");
        info.Data.DemoPasswordSet.Should().BeTrue();
        info.Data.StorePasswordSet.Should().BeTrue();
        jobs.Should().NotContain(j => j.Contains("S3cret-demo-pass"));
    }

    [Fact]
    public async Task StoreRateLimit_ShouldBeRetriedWithBackoff()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        Apple.Failures[$"/v1/apps/{appleId}/appStoreVersions"] = new([HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable]);

        var (http, listing) = await org.Owner.Client.GetAsync<ListingDto>($"listings?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}&locale=en-US");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        listing.Data.Fields.Should().Contain(f => f.Key == "keywords" && f.MaxBytes == 100);
        Apple.Requests.Count(r => r == $"GET /v1/apps/{appleId}/appStoreVersions").Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task StoreDown_ShouldAnswer502()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);

        Apple.Failures[$"/v1/apps/{appleId}"] = new(Enumerable.Repeat(HttpStatusCode.ServiceUnavailable, 10));

        var (http, body) = await org.Owner.Client.GetAsync<ListingDto>($"listings?organizationId={org.OrganizationId}&storeAppId={record.StoreAppId}");

        http.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        body.Messages.Should().Contain(m => m.Key == "Store.Unreachable");
    }

    [Fact]
    public async Task Readiness_ShouldListWhatBlocksTheSubmission()
    {
        // Arrange: iPhone and iPad app, no screenshots, no build attached.
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId, withBuild: false);
        var (record, _) = await NewAppAsync(org, appleId, profile: new AppProfile { DeviceFamilies = ["iphone", "ipad"], Features = [] });

        // Act
        var (http, report) = await org.Owner.Client.GetAsync<ReadinessReportDto>($"apps/readiness?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Apple}");
        var (_, overview) = await org.Owner.Client.GetAsync<List<AppOverviewDto>>($"apps/overview?organizationId={org.OrganizationId}");
        var (_, compliance) = await org.Owner.Client.GetAsync<List<ComplianceItemDto>>($"apps/compliance?organizationId={org.OrganizationId}&appId={record.AppId}");

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        report.Data.CanSubmit.Should().BeFalse();
        report.Data.Items.Should().Contain(i => i.Key == "build" && i.Status == ReadinessStatus.Missing);
        report.Data.Items.Should().Contain(i => i.Key == "texts.en-US" && i.Status == ReadinessStatus.Ok);

        var media = report.Data.Items.Single(i => i.Key == "screenshots");
        media.Status.Should().Be(ReadinessStatus.Missing);
        media.Slots.Should().Contain(s => s.ClassKey == "APP_IPAD_PRO_3GEN_129" && s.Required && s.FilledBy == MediaSlotFill.None);

        report.Data.Items.Should().Contain(i => i.Key == "assisted.apple.privacy.app-privacy" && i.Status == ReadinessStatus.Assisted);
        report.Data.Items.Should().Contain(i => i.Key == "assisted.apple.age-rating.shared-location-dating" && i.Status == ReadinessStatus.Assisted);

        overview.Data.Single(a => a.AppId == record.AppId).Stores.Single().Missing.Should().Be(report.Data.Missing);
        compliance.Data.Should().Contain(c => c.RuleId == "apple.age-rating.shared-location-dating" && c.DueDate == new DateOnly(2027, 1, 12) && c.Status == ComplianceStatus.Missing);
    }

    [Fact]
    public async Task BuildLinkAndAgeRating_ShouldBePlannedAndApplied()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        var versionId = Apple.AddApp(appleId, withBuild: false);
        var (record, _) = await NewAppAsync(org, appleId);
        var buildExternalId = Apple.Builds.Keys.Last();

        var buildRow = new Storeasy.Domain.Apps.Build { OrganizationId = org.OrganizationId, StoreAppId = record.StoreAppId, Store = StoreKind.Apple, ExternalId = buildExternalId, Version = "1.0", BuildNumber = "12", ProcessingState = BuildProcessingState.Valid, SyncedDate = DateTime.UtcNow };

        await _factory.DbAsync(async db =>
        {
            db.Builds.Add(buildRow);
            await db.SaveChangesAsync();
        });

        var (_, link) = await org.Owner.Client.PostAsync<ChangePlanDto>("builds/link", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, buildId = buildRow.Id, usesNonExemptEncryption = true });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, link.Data.PlanId);

        var (_, rating) = await org.Owner.Client.PutAsync<ChangePlanDto>("age-rating", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, answers = new { gambling = true }, dryRun = false });
        await WaitForJobAsync(rating.Data.JobId.Value);
        var (unknownHttp, _) = await org.Owner.Client.PutAsync<ChangePlanDto>("age-rating", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, answers = new { sharedLocation = true } });

        Apple.Versions[versionId]["build"].GetValue<string>().Should().Be(buildExternalId);
        Apple.Builds[buildExternalId]["usesNonExemptEncryption"].GetValue<bool>().Should().BeTrue();
        rating.Data.Status.Should().BeOneOf(ChangePlanStatus.Approved, ChangePlanStatus.Applied);
        Apple.AgeRatings.Values.Should().Contain(r => r["gambling"] != null && r["gambling"].GetValue<bool>());
        unknownHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest, "the new descriptors are not in the API yet");
    }
}
