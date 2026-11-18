using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Google Play content engine: edits, tracks, rollout, testers, bundles, data safety against the fake Play API.")]
public class GoogleContentTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private FakeGooglePlay Play => _factory.Stores.Google;

    private async Task<(Org Org, Record Record)> SetupAsync(AppProfile profile = null)
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.pusula";

        Play.AddApp(package);

        var (_, google) = await NewAppAsync(org, null, package, profile);

        return (org, google);
    }

    [Fact]
    public async Task ListingAndDetails_ShouldBeWrittenInOneCommittedEdit()
    {
        var (org, record) = await SetupAsync();

        var (_, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[]
            {
                new { locale = "en-US", field = "shortDescription", value = "Never get lost" },
                new { locale = "tr-TR", field = "title", value = "Pusula" },
            },
        });

        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);
        var (_, detailsPlan) = await org.Owner.Client.PostAsync<ChangePlanDto>("listings/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            changes = new[] { new { locale = (string)null, field = "contactPhone", value = "+905550000001" } },
        });

        applied.Status.Should().Be(ChangePlanStatus.Applied);
        Play.Apps[record.ExternalId]["listings"]["en-US"]["shortDescription"].GetValue<string>().Should().Be("Never get lost");
        Play.Apps[record.ExternalId]["listings"]["tr-TR"]["title"].GetValue<string>().Should().Be("Pusula");
        detailsPlan.Data.Kind.Should().Be(ChangePlanKind.Details);
        Play.OpenEdits.Should().Be(0, "every edit is committed or deleted");
    }

    [Fact]
    public async Task ProductionRelease_ShouldNeedReleaseProductionAndTheTypedConfirmation()
    {
        var (org, record) = await SetupAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);

        var (_, plan) = await editor.Client.PostAsync<ChangePlanDto>("releases/plan", new
        {
            organizationId = org.OrganizationId,
            storeAppId = record.StoreAppId,
            kind = ReleasePlanKind.AssignTrack,
            track = "production",
            name = "2.5.0",
            versionCodes = new[] { "10" },
            status = "inProgress",
            userFraction = 0.1,
            releaseNotes = new[] { new { language = "en-US", text = "Fixes." } },
            countries = new[] { "TR", "DE" },
        });

        var (editorApprove, _) = await editor.Client.PostAsync<ChangePlanDto>("releases/apply", new { organizationId = org.OrganizationId, planId = plan.Data.PlanId, confirm = "Pusula 2.5.0" });
        var (wrongConfirm, _) = await org.Owner.Client.PostAsync<ChangePlanDto>("releases/apply", new { organizationId = org.OrganizationId, planId = plan.Data.PlanId, confirm = "Pusula" });
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId, "Pusula 2.5.0");

        plan.Data.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        plan.Data.ConfirmText.Should().Be("Pusula 2.5.0");
        plan.Data.Warnings.Should().Contain(w => w.Message == "10", "the version code is not among the synced builds");
        editorApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongConfirm.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        applied.Status.Should().Be(ChangePlanStatus.Applied);

        var releases = Play.Apps[record.ExternalId]["tracks"]["production"]["releases"].AsArray();
        releases.Should().HaveCount(2, "the staged release keeps the completed one for the other users");
        releases[0]["status"].GetValue<string>().Should().Be("inProgress");
        releases[0]["userFraction"].GetValue<double>().Should().Be(0.1);
        releases[0]["countryTargeting"]["countries"].AsArray().Select(c => c.GetValue<string>()).Should().Equal("TR", "DE");
        releases[1]["status"].GetValue<string>().Should().Be("completed");
        Play.OpenEdits.Should().Be(0);
    }

    [Fact]
    public async Task Rollout_ShouldOnlyGrowAndHalt()
    {
        var (org, record) = await SetupAsync();

        var (lowerHttp, _) = await org.Owner.Client.PutAsync<ChangePlanDto>("releases/rollout", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, action = RolloutAction.SetPercentage, percentage = 10 });
        var (_, raise) = await org.Owner.Client.PutAsync<ChangePlanDto>("releases/rollout", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, action = RolloutAction.SetPercentage, percentage = 50, dryRun = false });
        var raised = await WaitForJobAsync(raise.Data.JobId.Value);
        var fraction = Play.Apps[record.ExternalId]["tracks"]["production"]["releases"][0]["userFraction"].GetValue<double>();
        var (_, halt) = await org.Owner.Client.PutAsync<ChangePlanDto>("releases/rollout", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, action = RolloutAction.Halt, reason = "Crash spike", dryRun = false });
        await WaitForJobAsync(halt.Data.JobId.Value);

        lowerHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        raised.Status.Should().Be(StoreJobStatus.Succeeded);
        fraction.Should().Be(0.5);
        Play.Apps[record.ExternalId]["tracks"]["production"]["releases"][0]["status"].GetValue<string>().Should().Be("halted");
    }

    [Fact]
    public async Task BundleUpload_ShouldUseTheResumableUploadAndPutTheVersionCodeOnTheTrack()
    {
        var (org, record) = await SetupAsync();
        var bundle = RandomNumberGenerator.GetBytes(700 * 1024);
        var assetId = await AssetAsync(org, bundle, "app-release.aab", "application/octet-stream", AssetPurpose.BuildArtifact);

        var (_, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("builds/bundle", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, assetId, track = "internal" });
        var applied = await ApproveAndWaitAsync(org.Owner, org.OrganizationId, plan.Data.PlanId);
        var steps = await _factory.DbAsync(db => db.StoreJobSteps.Where(s => s.StoreJobId == applied.JobId && s.Name == "bundle.upload").Select(s => new { s.Progress, s.Total }).SingleAsync());

        plan.Data.RequiredPermission.Should().Be(OrgPermission.ReleaseTest);
        applied.Status.Should().Be(ChangePlanStatus.Applied);
        Play.UploadSessions.Values.Should().Contain(chunks => chunks.Count == 3 && chunks.Sum(c => c.Length) == bundle.Length);

        var internalTrack = Play.Apps[record.ExternalId]["tracks"]["internal"]["releases"].AsArray().Single();
        internalTrack["versionCodes"].AsArray().Single().GetValue<string>().Should().Be("100");
        steps.Progress.Should().Be(bundle.Length);
        steps.Total.Should().Be(bundle.Length);
    }

    [Fact]
    public async Task TestersImagesAndDataSafety_ShouldBeWritten()
    {
        var (org, record) = await SetupAsync();
        var phone1 = await AssetAsync(org, Png(1080, 1920, salt: 1), "p1.png", "image/png");
        var phone2 = await AssetAsync(org, Png(1080, 1920, salt: 2), "p2.png", "image/png");
        var icon = await AssetAsync(org, Png(512, 512, alpha: true), "icon.png", "image/png");

        var (_, testers) = await org.Owner.Client.PutAsync<ChangePlanDto>("releases/testers", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, track = "alpha", googleGroups = new[] { "beta@googlegroups.com" }, dryRun = false });
        await WaitForJobAsync(testers.Data.JobId.Value);

        var (_, images) = await org.Owner.Client.PostAsync<ChangePlanDto>("screenshots/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, locale = "en-US", classKey = "phoneScreenshots", assetIds = new[] { phone1, phone2 } });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, images.Data.PlanId);

        var (_, iconPlan) = await org.Owner.Client.PostAsync<ChangePlanDto>("screenshots/plan", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId, locale = "en-US", classKey = "icon", assetIds = new[] { icon } });

        var (noAnswers, _) = await org.Owner.Client.PostAsync<ChangePlanDto>("apps/answers/data-safety", new { organizationId = org.OrganizationId, appId = record.AppId });
        await org.Owner.Client.PutAsync<int>("apps/answers", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            key = SavedAnswerKind.DataSafety,
            answers = new { rows = new[] { new { questionId = "PSL_DATA_COLLECTION_COLLECTS_PERSONAL_DATA", responseValue = "true", label = "Collects data, yes" } } },
        });
        var (_, safety) = await org.Owner.Client.PostAsync<ChangePlanDto>("apps/answers/data-safety", new { organizationId = org.OrganizationId, appId = record.AppId, dryRun = false });
        await WaitForJobAsync(safety.Data.JobId.Value);

        Play.Apps[record.ExternalId]["testers"]["alpha"]["googleGroups"].AsArray().Single().GetValue<string>().Should().Be("beta@googlegroups.com");
        Play.Apps[record.ExternalId]["images"]["en-US/phoneScreenshots"].AsArray().Should().HaveCount(2);
        iconPlan.Data.Files.Single().Result.Should().Be("Accepted", "the Play icon is a 32 bit PNG with alpha");
        noAnswers.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Play.DataSafety[record.ExternalId].Should().StartWith("Question ID (machine readable)").And.Contain("PSL_DATA_COLLECTION_COLLECTS_PERSONAL_DATA,,true,,\"Collects data, yes\"");
        Play.OpenEdits.Should().Be(0);
    }

    [Fact]
    public async Task Readiness_ShouldCheckTargetApiAndDataSafety()
    {
        var (org, record) = await SetupAsync(new AppProfile { TargetSdk = 34, IsLive = true, Features = [] });

        var (http, report) = await org.Owner.Client.GetAsync<ReadinessReportDto>($"apps/readiness?organizationId={org.OrganizationId}&appId={record.AppId}&store={StoreKind.Google}");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        report.Data.Items.Should().Contain(i => i.RuleId == "google.target-api.mobile-new-and-updates" && i.Status == ReadinessStatus.Missing);
        report.Data.Items.Should().Contain(i => i.Key == "data-safety" && i.Status == ReadinessStatus.Missing);
        report.Data.Items.Should().Contain(i => i.Key == "graphics" && i.Status == ReadinessStatus.Missing);
        report.Data.Items.Should().Contain(i => i.Key == "bundle" && i.Status == ReadinessStatus.Ok);
        Play.OpenEdits.Should().Be(0, "reads delete their edit");
    }
}
