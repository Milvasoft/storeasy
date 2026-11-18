using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Dtos.MockupDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Stores;
using Storeasy.Domain.Studio;
using Storeasy.IntegrationTests.TestBase;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Mockup studio: templates, captions, previews, export jobs and manifests.")]
public class MockupsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _medium = "apple.screenshots.iphone-dynamic-island-medium";
    private const string _feature = "google.graphics.feature-graphic";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(90);

    private sealed record Setup(TestUser Owner, int OrganizationId, int AppId);

    private async Task<Setup> NewSetupAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Studio {TestApi.Unique()}" });

        var appId = await _factory.DbAsync(async db =>
        {
            var app = new App { OrganizationId = organization.Data, Name = "Pusula" };

            db.Apps.Add(app);
            await db.SaveChangesAsync();

            return app.Id;
        });

        return new Setup(owner, organization.Data, appId);
    }

    private async Task<long> PutScreenshotAsync(int organizationId, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(new SKColor(0x46, 0x36, 0xFF));

        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);

        var key = AssetRules.ObjectKey(organizationId, AssetPurpose.Screenshot, "raw.png", Guid.NewGuid());

        _factory.ObjectStorage.Put(key, data.ToArray());

        return await _factory.DbAsync(async db =>
        {
            var asset = new Asset { OrganizationId = organizationId, Purpose = AssetPurpose.Screenshot, ObjectKey = key, FileName = "raw.png", ContentType = "image/png", Size = data.Size, Status = AssetStatus.Uploaded };

            db.Assets.Add(asset);
            await db.SaveChangesAsync();

            return asset.Id;
        });
    }

    private async Task<int> CreateTemplateAsync(Setup setup, string name = null, string[] locales = null)
    {
        var (http, body) = await setup.Owner.Client.PostAsync<int>("mockups/template", new
        {
            organizationId = setup.OrganizationId,
            appId = setup.AppId,
            name = name ?? $"Set {TestApi.Unique()}",
            locales = locales ?? ["tr", "en-US"],
            primaryLocale = "tr",
            slotCount = 3,
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK);

        return body.Data;
    }

    private async Task<MockupTemplateDetailDto> DetailAsync(Setup setup, int templateId)
        => (await setup.Owner.Client.GetAsync<MockupTemplateDetailDto>($"mockups/template?organizationId={setup.OrganizationId}&templateId={templateId}")).Body.Data;

    private Task<(HttpResponseMessage Http, Milvasoft.Components.Rest.MilvaResponse.Response<int> Body)> SaveTemplateAsync(Setup setup, MockupTemplateDetailDto template, object slots, int? version = null)
        => setup.Owner.Client.PutAsync<int>("mockups/template", new
        {
            organizationId = setup.OrganizationId,
            templateId = template.Id,
            version = version ?? template.Version,
            name = template.Name,
            spec = template.Spec,
            locales = template.Locales,
            primaryLocale = template.PrimaryLocale,
            slotCount = template.SlotCount,
            slots,
        });

    private async Task<StoreJob> WaitForEndAsync(long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().Include(j => j.Steps).SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task CreateFromStarter_ShouldCopyDesignAndSampleCaptionsPerLanguage()
    {
        // Arrange
        var setup = await NewSetupAsync();

        // Act
        var (startersHttp, starters) = await setup.Owner.Client.GetAsync<List<MockupStarterTemplateDto>>($"mockups/starter-templates?organizationId={setup.OrganizationId}");
        var templateId = await CreateTemplateAsync(setup, locales: ["tr", "en-US", "de-DE"]);
        var detail = await DetailAsync(setup, templateId);
        var (listHttp, list) = await setup.Owner.Client.PatchListAsync<MockupTemplateListDto>("mockups/templates", new { organizationId = setup.OrganizationId, appId = setup.AppId, pageNumber = 1, rowCount = 10 });
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().CountAsync(a => a.OrganizationId == setup.OrganizationId && a.Action == AuditAction.MockupTemplateCreated));

        // Assert
        startersHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        starters.Data.Select(s => s.Key).Should().Contain(["ultramarine", "paper", "night", "mint"]);
        detail.StarterKey.Should().Be("ultramarine");
        detail.Version.Should().Be(1);
        detail.Spec.Background.Colors.Should().Equal("#4636FF", "#1B1464");
        detail.Captions.Where(c => c.Locale == "tr").Should().HaveCount(3);
        detail.Captions.Where(c => c.Locale == "en-US").Should().HaveCount(3, "en-US takes the en samples");
        detail.Captions.Should().NotContain(c => c.Locale == "de-DE");
        listHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Data.Should().ContainSingle(t => t.Id == templateId && t.AppName == "Pusula");
        audit.Should().Be(1);
    }

    [Fact]
    public async Task Create_ShouldRefuseDuplicatesUnknownAppsAndBadInput()
    {
        var setup = await NewSetupAsync();
        await CreateTemplateAsync(setup, "Store set 2026");

        var (duplicate, _) = await setup.Owner.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = setup.AppId, name = "Store set 2026", locales = new[] { "tr" }, primaryLocale = "tr" });
        var (unknownApp, _) = await setup.Owner.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = 999999, name = "x", locales = new[] { "tr" }, primaryLocale = "tr" });
        var (badLocale, _) = await setup.Owner.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = setup.AppId, name = "y", locales = new[] { "TR_tr" }, primaryLocale = "TR_tr" });
        var (primaryMissing, _) = await setup.Owner.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = setup.AppId, name = "z", locales = new[] { "tr" }, primaryLocale = "en" });
        var (unknownStarter, _) = await setup.Owner.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = setup.AppId, name = "w", locales = new[] { "tr" }, primaryLocale = "tr", starterKey = "nope" });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        unknownApp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        badLocale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        primaryMissing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        unknownStarter.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ShouldReplaceSlotsBumpTheVersionAndRefuseStaleSaves()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);
        var detail = await DetailAsync(setup, templateId);
        var assetId = await PutScreenshotAsync(setup.OrganizationId, 1206, 2622);
        var other = await NewSetupAsync();
        var foreignAsset = await PutScreenshotAsync(other.OrganizationId, 1206, 2622);

        detail.Spec.Frame.Cutout = "hole";
        detail.Spec.Background = new MockupBackgroundSpec { Kind = "solid", Colors = ["#F5F3EE"] };

        // Act
        var (saved, version) = await SaveTemplateAsync(setup, detail, new[] { new { order = 1, family = MockupDeviceFamily.Phone, assetId } });
        var (stale, _) = await SaveTemplateAsync(setup, detail, Array.Empty<object>(), version: 1);
        var (foreign, _) = await SaveTemplateAsync(setup, detail, new[] { new { order = 1, family = MockupDeviceFamily.Phone, assetId = foreignAsset } }, version: 2);
        detail.Spec.Frame.Scale = 9;
        var (invalidSpec, invalidBody) = await SaveTemplateAsync(setup, detail, Array.Empty<object>(), version: 2);
        var after = await DetailAsync(setup, templateId);

        // Assert
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        version.Data.Should().Be(2);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidSpec.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidBody.Messages.Should().Contain(m => m.Message.Contains("frame.scale"));
        after.Version.Should().Be(2);
        after.Spec.Frame.Cutout.Should().Be("hole");
        after.Spec.Background.Kind.Should().Be("solid");
        after.Slots.Should().ContainSingle(s => s.AssetId == assetId && s.Order == 1);
    }

    [Fact]
    public async Task Preview_ShouldRenderSmallPngsFromTheStoredOrAnUnsavedDesign()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);
        var detail = await DetailAsync(setup, templateId);
        var assetId = await PutScreenshotAsync(setup.OrganizationId, 1206, 2622);
        await SaveTemplateAsync(setup, detail, new[] { new { order = 1, family = MockupDeviceFamily.Phone, assetId } });

        var unsaved = MockupSpecRules.CreateDefault();
        unsaved.Background = new MockupBackgroundSpec { Kind = "solid", Colors = ["#000000"] };

        // Act
        var (http, preview) = await setup.Owner.Client.PostAsync<MockupPreviewDto>("mockups/render-preview", new { organizationId = setup.OrganizationId, templateId, locale = "en-US", classKey = _medium, slotOrders = new[] { 1, 2 }, maxWidth = 300 });
        var (_, dark) = await setup.Owner.Client.PostAsync<MockupPreviewDto>("mockups/render-preview", new { organizationId = setup.OrganizationId, templateId, slotOrders = new[] { 1 }, spec = unsaved, maxWidth = 200 });
        var (badLocale, _) = await setup.Owner.Client.PostAsync<MockupPreviewDto>("mockups/render-preview", new { organizationId = setup.OrganizationId, templateId, locale = "fr" });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Data.Images.Should().HaveCount(2);
        preview.Data.Images[0].Width.Should().Be(300);
        preview.Data.Images[0].Url.Should().StartWith("data:image/png;base64,");
        preview.Data.Images[1].Warnings.Should().Contain(w => w.Code == MockupWarningCodes.ScreenshotMissing);

        using var image = SKBitmap.Decode(Convert.FromBase64String(dark.Data.Images[0].Url["data:image/png;base64,".Length..]));

        image.Width.Should().Be(200);
        image.GetPixel(2, 2).Should().Be(SKColors.Black, "the unsaved design is drawn");
        dark.Data.Locale.Should().Be("tr", "the primary locale is the default");
        badLocale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Captions_ShouldSaveRemoveAndReportOverflow()
    {
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);

        var (http, saved) = await setup.Owner.Client.PutAsync<MockupCaptionsSavedDto>("mockups/captions", new
        {
            organizationId = setup.OrganizationId,
            templateId,
            items = new object[]
            {
                new { locale = "en-US", slotOrder = 1, title = "Check off every single step and keep track of all of them, every day, every week and every month", subtitle = "Small steps" },
                new { locale = "en-US", slotOrder = 2, title = "", subtitle = "" },
            },
        });
        var (unknownLocale, _) = await setup.Owner.Client.PutAsync<MockupCaptionsSavedDto>("mockups/captions", new { organizationId = setup.OrganizationId, templateId, items = new[] { new { locale = "fr", slotOrder = 1, title = "Bonjour" } } });
        var (outOfRange, _) = await setup.Owner.Client.PutAsync<MockupCaptionsSavedDto>("mockups/captions", new { organizationId = setup.OrganizationId, templateId, items = new[] { new { locale = "tr", slotOrder = 3 + 1, title = "x" } } });
        var detail = await DetailAsync(setup, templateId);

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        saved.Data.Saved.Should().Be(2);
        saved.Data.MeasuredClassKey.Should().NotBeNullOrEmpty();
        saved.Data.Warnings.Should().ContainSingle(w => w.Code == MockupWarningCodes.CaptionOverflow && w.Locale == "en-US" && w.SlotOrder == 1 && w.Field == "title");
        detail.Captions.Should().NotContain(c => c.Locale == "en-US" && c.SlotOrder == 2, "an empty cell removes its row");
        detail.Captions.Single(c => c.Locale == "en-US" && c.SlotOrder == 1).Subtitle.Should().Be("Small steps");
        unknownLocale.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        outOfRange.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SuggestCaptions_WithoutATranslator_ShouldBe503()
    {
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);

        var (http, _) = await setup.Owner.Client.PostAsync<MockupCaptionSuggestionDto>("mockups/captions/suggest", new { organizationId = setup.OrganizationId, templateId, targetLocale = "en-US" });

        http.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task FramesAndPlan_ShouldComeFromTheRulesCatalog()
    {
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);

        var (framesHttp, frames) = await setup.Owner.Client.GetAsync<List<MockupFrameDto>>($"mockups/frames?organizationId={setup.OrganizationId}");
        var (planHttp, plan) = await setup.Owner.Client.PostAsync<MockupExportPlanDto>("mockups/export/plan", new
        {
            organizationId = setup.OrganizationId,
            templateId,
            targets = new[] { new { classKey = _medium, width = 1320, height = 2868 } },
        });
        var (unknownClass, _) = await setup.Owner.Client.PostAsync<MockupExportPlanDto>("mockups/export/plan", new { organizationId = setup.OrganizationId, templateId, classKeys = new[] { "apple.screenshots.nope" } });

        framesHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        frames.Data.Single(f => f.Kind == MockupFrameKind.FoldableInner).Classes.Should().ContainSingle(c => c.Width == 2007 && c.Height == 2853);
        frames.Data.Single(f => f.Kind == MockupFrameKind.FoldableOuter).Classes.Should().ContainSingle(c => c.Width == 1398 && c.Height == 2034);
        frames.Data.Single(f => f.Kind == MockupFrameKind.Phone).Cutouts.Should().Contain(["island", "notch", "hole", "none"]);
        planHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        plan.Data.Rejected.Should().ContainSingle(r => r.ClassKey == _medium && r.Width == 1320);
        plan.Data.Items.Should().Contain(i => i.Store == StoreKind.Microsoft && i.Width == 1920 && i.Height == 1080);
        plan.Data.Total.Should().Be(plan.Data.Items.Where(i => i.Selected).Sum(i => i.PerLocaleCount) * 2);
        unknownClass.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Export_ShouldRenderStoreZipAndDescribeTheImagesInTheManifest()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);
        var detail = await DetailAsync(setup, templateId);
        var phone = await PutScreenshotAsync(setup.OrganizationId, 1206, 2622);
        var tiny = await PutScreenshotAsync(setup.OrganizationId, 300, 652);

        await SaveTemplateAsync(setup, detail, new object[]
        {
            new { order = 1, family = MockupDeviceFamily.Phone, assetId = phone },
            new { order = 2, family = MockupDeviceFamily.Phone, assetId = phone },
            new { order = 3, family = MockupDeviceFamily.Phone, assetId = tiny },
        });

        // Act
        var (http, job) = await setup.Owner.Client.PostWithKeyAsync<long>("mockups/export", new { organizationId = setup.OrganizationId, templateId, locales = new[] { "tr", "en-US" }, classKeys = new[] { _medium, _feature } }, Guid.NewGuid().ToString());
        var finished = await WaitForEndAsync(job.Data);
        var (manifestHttp, manifest) = await setup.Owner.Client.GetAsync<MockupExportManifest>($"mockups/export/manifest?organizationId={setup.OrganizationId}&jobId={job.Data}");
        var (zipHttp, zip) = await setup.Owner.Client.GetAsync<AssetDownloadDto>($"mockups/export/zip?organizationId={setup.OrganizationId}&jobId={job.Data}");
        var after = await DetailAsync(setup, templateId);
        var assets = await _factory.DbAsync(db => db.Assets.AsNoTracking().Where(a => a.OrganizationId == setup.OrganizationId && a.Purpose == AssetPurpose.MockupOutput).ToListAsync());

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        finished.Status.Should().Be(StoreJobStatus.Succeeded, finished.Error);
        finished.Steps.Where(s => s.Name == "render.size").Should().HaveCount(2);
        finished.Steps.Where(s => s.Name == "render.size").OrderBy(s => s.Order).Select(s => s.Status).Should().Equal([StoreJobStepStatus.Failed, StoreJobStepStatus.Succeeded], "slot 3 is too small for the phone class; the feature graphic has no screenshot");
        finished.Steps.Should().Contain(s => s.Name == "zip.store" && s.Status == StoreJobStepStatus.Succeeded);

        manifestHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        manifest.Data.JobId.Should().Be(job.Data);
        manifest.Data.Sets.Should().HaveCount(4, "two classes in two locales");
        manifest.Data.Sets.Single(s => s.ClassKey == _medium && s.Locale == "tr").Images.Select(i => i.SlotOrder).Should().Equal(1, 2);
        manifest.Data.Sets.Single(s => s.ClassKey == _feature && s.Locale == "en-US").Should().Match<MockupExportSet>(s => s.Width == 1024 && s.Height == 500 && s.Images.Count == 1);
        manifest.Data.Failures.Should().HaveCount(2).And.OnlyContain(f => f.Code == MockupWarningCodes.AssetTooSmall && f.SlotOrder == 3);
        manifest.Data.ImageCount.Should().Be(6);

        var images = manifest.Data.Sets.SelectMany(s => s.Images).ToList();

        assets.Select(a => a.Id).Should().Contain(images.Select(i => i.AssetId)).And.Contain(manifest.Data.ZipAssetId.Value);

        foreach (var image in images)
        {
            var bytes = _factory.ObjectStorage.Objects[assets.Single(a => a.Id == image.AssetId).ObjectKey];

            bytes[25].Should().Be(2, "exported PNGs have no alpha channel");
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)).Should().Be(image.Sha256);
        }

        using (var archive = new ZipArchive(new MemoryStream(_factory.ObjectStorage.Objects[assets.Single(a => a.Id == manifest.Data.ZipAssetId).ObjectKey])))
        {
            archive.Entries.Select(e => e.FullName).Should().Contain(["manifest.json", "apple/iphone-dynamic-island-medium/tr/01.png", "google/feature-graphic/en-US/01.png"]);

            using var reader = new StreamReader(archive.GetEntry("manifest.json").Open());

            JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("sets").GetArrayLength().Should().Be(4);
        }

        zipHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        zip.Data.Url.Should().Contain($"mockups-{job.Data}.zip");
        after.LastExportJobId.Should().Be(job.Data);
        after.LastExportImageCount.Should().Be(6);
    }

    [Fact]
    public async Task Export_WithNothingToRender_AndUnfinishedManifest_ShouldBeRefused()
    {
        var setup = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);

        var (empty, _) = await setup.Owner.Client.PostAsync<long>("mockups/export", new { organizationId = setup.OrganizationId, templateId, classKeys = Array.Empty<string>() });
        var (missing, _) = await setup.Owner.Client.GetAsync<MockupExportManifest>($"mockups/export/manifest?organizationId={setup.OrganizationId}&jobId=999999");

        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Roles_ViewerMayReadAndPreviewButNotWrite_StrangersSeeNothing()
    {
        // Arrange
        var setup = await NewSetupAsync();
        var stranger = await NewSetupAsync();
        var templateId = await CreateTemplateAsync(setup);
        var viewerUser = await _factory.CreateUserAsync(email: $"v{TestApi.Unique()}@example.com");
        var viewer = await _factory.LoginAsync(viewerUser.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await setup.Owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = setup.OrganizationId, email = viewerUser.Email, role = OrgRole.Viewer });
        await viewer.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        // Act
        var (viewerRead, _) = await viewer.Client.GetAsync<MockupTemplateDetailDto>($"mockups/template?organizationId={setup.OrganizationId}&templateId={templateId}");
        var (viewerPreview, _) = await viewer.Client.PostAsync<MockupPreviewDto>("mockups/render-preview", new { organizationId = setup.OrganizationId, templateId, maxWidth = 150 });
        var (viewerCreate, _) = await viewer.Client.PostAsync<int>("mockups/template", new { organizationId = setup.OrganizationId, appId = setup.AppId, name = "v", locales = new[] { "tr" }, primaryLocale = "tr" });
        var (viewerExport, _) = await viewer.Client.PostAsync<long>("mockups/export", new { organizationId = setup.OrganizationId, templateId });
        var (strangerRead, _) = await stranger.Owner.Client.GetAsync<MockupTemplateDetailDto>($"mockups/template?organizationId={stranger.OrganizationId}&templateId={templateId}");
        var (strangerOrg, _) = await stranger.Owner.Client.GetAsync<MockupTemplateDetailDto>($"mockups/template?organizationId={setup.OrganizationId}&templateId={templateId}");
        var (deleted, _) = await setup.Owner.Client.DeleteAsync<int>($"mockups/template?organizationId={setup.OrganizationId}&templateId={templateId}");
        var (afterDelete, _) = await setup.Owner.Client.GetAsync<MockupTemplateDetailDto>($"mockups/template?organizationId={setup.OrganizationId}&templateId={templateId}");

        // Assert
        viewerRead.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerPreview.StatusCode.Should().Be(HttpStatusCode.OK);
        viewerCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        viewerExport.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        strangerRead.StatusCode.Should().Be(HttpStatusCode.NotFound);
        strangerOrg.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
