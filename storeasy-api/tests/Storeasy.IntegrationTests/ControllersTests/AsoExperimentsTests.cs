using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Store listing experiments: App Store experiments and custom product pages through plans, Play assisted records.")]
public class AsoExperimentsTests(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private const string _ipad = "APP_IPAD_PRO_3GEN_129";

    private FakeAppStoreConnect Apple => _factory.Stores.Apple;

    [Fact]
    public async Task Experiment_ShouldBeWrittenSubmittedStartedAndStoppedThroughApprovedPlans()
    {
        // Arrange
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var editor = await MemberAsync(org, OrgRole.Editor);
        var shot = await AssetAsync(org, Png(2064, 2752), "bold-1.png", "image/png");

        // Act 1: a draft with an icon treatment and a screenshot treatment, then the write plan.
        var (savedHttp, saved) = await editor.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            storeAppId = record.StoreAppId,
            kind = AsoExperimentKind.ProductPageOptimization,
            name = "Icon test",
            trafficProportion = 30,
            variants = new object[]
            {
                new { key = "A", name = "Dark icon", appIconName = "AppIconDark", locales = Array.Empty<object>() },
                new { key = "B", name = "Bold screenshots", locales = new[] { new { locale = "en-US", media = new[] { new { kind = MediaKind.Screenshot, classKey = _ipad, assetIds = new[] { shot } } } } } },
            },
        });
        var id = saved.Data.Id;
        var (_, push) = await editor.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id, operation = AsoExperimentOperation.Push });
        var pushed = await ApproveAndWaitAsync(editor, org.OrganizationId, push.Data.PlanId);
        var (_, prepared) = await editor.Client.GetAsync<AsoExperimentDto>($"aso-experiments/experiment?organizationId={org.OrganizationId}&id={id}");

        // Assert 1: one experiment with two treatments; the screenshot went through reserve and commit under the
        // treatment localization.
        savedHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.Data.Status.Should().Be(AsoExperimentStatus.Draft);
        saved.Data.Operations.Should().Equal(AsoExperimentOperation.Push);
        push.Data.RequiredPermission.Should().Be(OrgPermission.EditContent);
        push.Data.ConfirmText.Should().BeNull();
        pushed.Status.Should().Be(ChangePlanStatus.Applied);

        var experiment = Apple.Experiments.Single(e => (string)e.Value["app"] == appleId);
        experiment.Value["trafficProportion"].GetValue<int>().Should().Be(30);
        Apple.Treatments.Values.Where(t => (string)t["experiment"] == experiment.Key).Select(t => (string)t["appIconName"]).Should().BeEquivalentTo(["AppIconDark", null]);
        var treatmentLocalization = Apple.TreatmentLocalizations.Single(l => (string)l.Value["locale"] == "en-US" && Apple.Treatments[(string)l.Value["treatment"]]["experiment"]!.GetValue<string>() == experiment.Key);
        Apple.ScreenshotSets.Values.Should().ContainSingle(s => (string)s["locId"] == treatmentLocalization.Key && (string)s["screenshotDisplayType"] == _ipad);
        Apple.Screenshots.Values.Single(s => (string)s["fileName"] == "bold-1.png")["state"].GetValue<string>().Should().Be("COMPLETE");

        prepared.Data.Status.Should().Be(AsoExperimentStatus.Prepared);
        prepared.Data.ExternalId.Should().Be(experiment.Key);
        prepared.Data.Variants.Should().OnlyContain(v => v.ExternalId != null);
        prepared.Data.Operations.Should().Contain(AsoExperimentOperation.Submit);
        prepared.Data.Timeline.Should().Contain(t => t.Kind == "plan" && t.PlanId == push.Data.PlanId && t.PlanStatus == ChangePlanStatus.Applied);

        // Act 2: submit; an editor may make the plan but not approve it, and the owner must type the confirmation.
        var (_, submit) = await editor.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id, operation = AsoExperimentOperation.Submit });
        var (editorApproveHttp, _) = await editor.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = org.OrganizationId, planId = submit.Data.PlanId, confirm = "Pusula Icon test" });
        var (wrongHttp, wrong) = await org.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = org.OrganizationId, planId = submit.Data.PlanId, confirm = "Pusula" });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, submit.Data.PlanId, "Pusula Icon test");

        // Assert 2
        submit.Data.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        submit.Data.ConfirmText.Should().Be("Pusula Icon test");
        editorApproveHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        wrong.Messages.Should().Contain(m => m.Key == "Plan.ConfirmMismatch");
        Apple.SubmittedItems.Should().Contain(("appStoreVersionExperiments", experiment.Key));
        experiment.Value["state"].GetValue<string>().Should().Be("WAITING_FOR_REVIEW");
        (await RecordAsync(id)).Status.Should().Be(AsoExperimentStatus.InReview);

        // Act 3: App Review approves it in the store; the sync reads it, then the owner starts and stops it.
        experiment.Value["state"] = "APPROVED";
        var (_, sync) = await editor.Client.PostAsync<long>("aso-experiments/sync", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId });
        (await WaitForJobAsync(sync.Data)).Status.Should().Be(StoreJobStatus.Succeeded);
        (await RecordAsync(id)).Status.Should().Be(AsoExperimentStatus.Approved);

        var (_, start) = await editor.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id, operation = AsoExperimentOperation.Start });
        var (editorStartHttp, _) = await editor.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = org.OrganizationId, planId = start.Data.PlanId, confirm = "Pusula Icon test" });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, start.Data.PlanId, "Pusula Icon test");
        var running = await RecordAsync(id);

        var (_, stop) = await org.Owner.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id, operation = AsoExperimentOperation.Stop });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, stop.Data.PlanId, "Pusula Icon test");
        var stopped = await RecordAsync(id);

        // Assert 3
        start.Data.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        editorStartHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        running.Status.Should().Be(AsoExperimentStatus.Running);
        running.StartDate.Should().NotBeNull();
        stopped.Status.Should().Be(AsoExperimentStatus.Stopped);
        stopped.EndDate.Should().NotBeNull();
        Apple.ExperimentWrites.Where(w => w.Method == "PATCH" && w.Path == $"/v2/appStoreVersionExperiments/{experiment.Key}" && w.Body["data"]["attributes"]["started"] != null)
             .Select(w => w.Body["data"]["attributes"]["started"].GetValue<bool>()).Should().Equal(true, false);

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());
        audit.Should().Contain([AuditAction.AsoExperimentCreated, AuditAction.AsoExperimentWritten, AuditAction.AsoExperimentSubmitted, AuditAction.AsoExperimentStarted, AuditAction.AsoExperimentStopped]);
    }

    [Fact]
    public async Task Approval_AfterTheExperimentChangedInAppStoreConnect_ShouldBeAConflict()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var externalId = Apple.AddExperiment(appleId, "Made in ASC", "APPROVED");

        var (_, sync) = await org.Owner.Client.PostAsync<long>("aso-experiments/sync", new { organizationId = org.OrganizationId, storeAppId = record.StoreAppId });
        await WaitForJobAsync(sync.Data);
        var (_, overview) = await org.Owner.Client.GetAsync<AsoExperimentOverviewDto>($"aso-experiments?organizationId={org.OrganizationId}&appId={record.AppId}");
        var found = overview.Data.Experiments.Single();

        var (_, start) = await org.Owner.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id = found.Id, operation = AsoExperimentOperation.Start });
        Apple.Experiments[externalId]["state"] = "STOPPED";
        var (http, conflict) = await org.Owner.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId = org.OrganizationId, planId = start.Data.PlanId, confirm = "Pusula Made in ASC" });

        found.Name.Should().Be("Made in ASC");
        found.Status.Should().Be(AsoExperimentStatus.Approved);
        overview.Data.Capabilities.Should().ContainSingle(c => c.Store == StoreKind.Apple && c.Kinds.Count == 2 && c.Kinds.All(k => k.Support == AsoStoreSupport.Api));
        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
        conflict.Messages.Should().Contain(m => m.Key == "Plan.Conflict");
        Apple.ExperimentWrites.Should().NotContain(w => w.Path.Contains(externalId));
    }

    [Fact]
    public async Task CustomProductPage_ShouldBeCreatedInlineAndEditedThroughANewVersionOnceLive()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var shot = await AssetAsync(org, Png(2064, 2752), "students.png", "image/png");

        var (_, saved) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            storeAppId = record.StoreAppId,
            kind = AsoExperimentKind.CustomProductPage,
            name = "Students",
            variants = new[]
            {
                new { key = "A", name = "Students", locales = new[] { new { locale = "en-US", promotionalText = "Study smarter.", media = new[] { new { kind = MediaKind.Screenshot, classKey = _ipad, assetIds = new[] { shot } } } } } },
            },
        });
        var (_, push) = await org.Owner.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id = saved.Data.Id, operation = AsoExperimentOperation.Push });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, push.Data.PlanId);
        var created = await RecordAsync(saved.Data.Id);
        var page = Apple.CustomPages.Single(p => (string)p.Value["app"] == appleId);

        created.ExternalId.Should().Be(page.Key);
        created.Url.Should().Contain($"ppid={page.Key}");
        created.Status.Should().Be(AsoExperimentStatus.Prepared);
        var create = Apple.ExperimentWrites.Single(w => w.Method == "POST" && w.Path == "/v1/appCustomProductPages");
        create.Body["included"].AsArray().Should().HaveCount(2, "the first version and localization go inline");
        Apple.CustomPageLocalizations.Values.Should().ContainSingle(l => (string)l["promotionalText"] == "Study smarter.");

        // The page goes live; editing it makes a new version that starts with the live content.
        foreach (var version in Apple.CustomPageVersions.Values.Where(v => (string)v["page"] == page.Key))
            version["state"] = "APPROVED";

        var (_, edited) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            id = saved.Data.Id,
            appId = record.AppId,
            name = "Students",
            variants = new[] { new { key = "A", name = "Students", locales = new[] { new { locale = "en-US", promotionalText = "Study faster." } } } },
        });
        var (_, again) = await org.Owner.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id = saved.Data.Id, operation = AsoExperimentOperation.Push });
        await ApproveAndWaitAsync(org.Owner, org.OrganizationId, again.Data.PlanId);

        edited.Data.Should().NotBeNull();
        again.Data.Items.Should().ContainSingle(i => i.Field == "promotionalText" && i.Before == "Study smarter." && i.After == "Study faster.");
        Apple.CustomPageVersions.Values.Count(v => (string)v["page"] == page.Key).Should().Be(2);
        Apple.ExperimentWrites.Should().Contain(w => w.Method == "POST" && w.Path == "/v1/appCustomProductPageVersions");
        (await RecordAsync(saved.Data.Id)).StoreState.Should().Be("PREPARE_FOR_SUBMISSION");
    }

    [Fact]
    public async Task PlayExperiment_ShouldBeAnAssistedRecordWithChecklistResultAndExport()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.pusula";
        var (_, google) = await NewAppAsync(org, null, package);
        var feature = await AssetAsync(org, Png(1024, 500), "feature.png", "image/png", AssetPurpose.Graphic);

        var (wrongKindHttp, wrongKind) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            storeAppId = google.StoreAppId,
            kind = AsoExperimentKind.ProductPageOptimization,
            name = "Nope",
            trafficProportion = 50,
            variants = new[] { new { key = "A", name = "A", appIconName = "X" } },
        });
        var (_, saved) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            storeAppId = google.StoreAppId,
            kind = AsoExperimentKind.PlayListingExperiment,
            name = "Short description test",
            trafficProportion = 50,
            variants = new[]
            {
                new { key = "A", name = "Benefit first", locales = new[] { new { locale = "tr-TR", shortDescription = "Yolunu kolayca bul.", media = new[] { new { kind = MediaKind.Screenshot, classKey = "featureGraphic", assetIds = new[] { feature } } } } } },
            },
        });
        var id = saved.Data.Id;
        var (planHttp, plan) = await org.Owner.Client.PostAsync<ChangePlanDto>("aso-experiments/experiment/plan", new { organizationId = org.OrganizationId, id, operation = AsoExperimentOperation.Push });
        var (_, checklist) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment/checklist", new { organizationId = org.OrganizationId, id, doneItems = new[] { 1, 2, 3 } });
        var (badChecklistHttp, _) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment/checklist", new { organizationId = org.OrganizationId, id, doneItems = new[] { 9 } });
        var (_, running) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment/result", new { organizationId = org.OrganizationId, id, status = AsoExperimentStatus.Running });
        var (_, finished) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment/result", new { organizationId = org.OrganizationId, id, status = AsoExperimentStatus.Completed, winnerVariantKey = "A", resultSummary = "+4% installs", resultUrl = "https://play.google.com/console/report" });
        var (badWinnerHttp, _) = await org.Owner.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment/result", new { organizationId = org.OrganizationId, id, winnerVariantKey = "Z" });
        var (_, export) = await org.Owner.Client.GetAsync<AsoExperimentExportDto>($"aso-experiments/experiment/export?organizationId={org.OrganizationId}&id={id}");
        var (_, syncAttempt) = await org.Owner.Client.PostAsync<long>("aso-experiments/sync", new { organizationId = org.OrganizationId, storeAppId = google.StoreAppId });

        wrongKindHttp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        wrongKind.Messages.Should().Contain(m => m.Key == "AsoKindNotSupported");
        saved.Data.Assisted.Steps.Should().HaveCount(6);
        saved.Data.Assisted.Url.Should().StartWith("https://play.google.com/console");
        saved.Data.Operations.Should().BeEmpty();
        planHttp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        plan.Messages.Should().Contain(m => m.Key == "AsoExperimentNoApi");
        checklist.Data.ChecklistDone.Should().Equal(1, 2, 3);
        badChecklistHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        running.Data.Status.Should().Be(AsoExperimentStatus.Running);
        running.Data.StartDate.Should().NotBeNull();
        finished.Data.Status.Should().Be(AsoExperimentStatus.Completed);
        finished.Data.EndDate.Should().NotBeNull();
        finished.Data.WinnerVariantKey.Should().Be("A");
        finished.Data.Timeline.Select(t => t.Kind).Should().Contain(["created", "started", "ended", "result"]);
        badWinnerHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        export.Data.Texts.Should().ContainSingle(t => t.Field == "shortDescription" && t.Value == "Yolunu kolayca bul.");
        export.Data.Files.Should().ContainSingle(f => f.AssetId == feature && f.ClassKey == "featureGraphic" && f.Url != null);
        syncAttempt.IsSuccess.Should().BeFalse();

        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId).Select(a => a.Action).ToListAsync());
        audit.Should().Contain([AuditAction.AsoExperimentCreated, AuditAction.AsoExperimentUpdated, AuditAction.AsoExperimentResultRecorded]);

        var (deleteHttp, deleted) = await org.Owner.Client.SendAsync<bool>(HttpMethod.Delete, $"aso-experiments/experiment?organizationId={org.OrganizationId}&id={id}", null);

        deleteHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        deleted.Data.Should().BeTrue();
    }

    [Fact]
    public async Task Viewer_ShouldReadButNotCreate()
    {
        var org = await NewOrgAsync();
        var appleId = $"a{org.Suffix}";
        Apple.AddApp(appleId);
        var (record, _) = await NewAppAsync(org, appleId);
        var viewer = await MemberAsync(org, OrgRole.Viewer);

        var (readHttp, overview) = await viewer.Client.GetAsync<AsoExperimentOverviewDto>($"aso-experiments?organizationId={org.OrganizationId}&appId={record.AppId}");
        var (writeHttp, _) = await viewer.Client.PostAsync<AsoExperimentDto>("aso-experiments/experiment", new
        {
            organizationId = org.OrganizationId,
            appId = record.AppId,
            storeAppId = record.StoreAppId,
            kind = AsoExperimentKind.ProductPageOptimization,
            name = "Icon test",
            trafficProportion = 30,
            variants = new[] { new { key = "A", name = "A", appIconName = "X" } },
        });

        readHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        overview.Data.Experiments.Should().BeEmpty();
        writeHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<Domain.Experiments.AsoExperiment> RecordAsync(int id)
        => _factory.DbAsync(db => db.AsoExperiments.AsNoTracking().SingleAsync(e => e.Id == id));
}
