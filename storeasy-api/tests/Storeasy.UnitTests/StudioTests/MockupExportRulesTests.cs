using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Studio;
using Storeasy.Infrastructure.Studio;

namespace Storeasy.UnitTests.StudioTests;

[Trait("Studio Unit Tests", "Export rules and planning.")]
public class MockupExportRulesTests
{
    private static readonly DateOnly _today = new(2026, 11, 18);

    private static ScreenshotSizeCatalog Catalog => StudioTestImages.Catalog;

    private static MockupTemplateSnapshot Template(int slotCount = 6, params string[] locales) => new()
    {
        Id = 7,
        AppId = 3,
        Name = "Store set",
        PrimaryLocale = locales.Length > 0 ? locales[0] : "tr",
        Locales = locales.Length > 0 ? [.. locales] : ["tr", "en-US", "de-DE"],
        SlotCount = slotCount,
        Version = 1,
        Spec = MockupSpecRules.CreateDefault(),
        Slots = [],
        Captions = [],
    };

    [Fact]
    public void Requirement_DuoBeforeApril2027_ShouldBeRecommendedThenRequired()
    {
        var duo = Catalog.Find("apple.screenshots.iphone-duo-inner");

        MockupExportRules.Requirement(duo, _today, []).Should().Be(MockupRequirement.Recommended);
        MockupExportRules.Requirement(duo, new DateOnly(2027, 4, 1), []).Should().Be(MockupRequirement.Required);
    }

    [Fact]
    public void Requirement_FaceIdLarge_ShouldBeOptionalWhenDynamicIslandLargeIsProvided()
    {
        var faceId = Catalog.Find("apple.screenshots.iphone-face-id-large");

        MockupExportRules.Requirement(faceId, _today, ["apple.screenshots.iphone-dynamic-island-large"]).Should().Be(MockupRequirement.Optional);
        MockupExportRules.Requirement(faceId, _today, []).Should().Be(MockupRequirement.Required);
    }

    [Fact]
    public void Accepts_The69InchSizeInThe61InchClass_ShouldBeRefused()
    {
        var medium = Catalog.Find("apple.screenshots.iphone-dynamic-island-medium");

        MockupExportRules.Accepts(medium, new PixelSize(1320, 2868)).Should().BeFalse();
        MockupExportRules.Accepts(medium, new PixelSize(1179, 2556)).Should().BeTrue();
        MockupExportRules.Accepts(Catalog.Find("google.screenshots.phone"), new PixelSize(4000, 2000)).Should().BeFalse();
    }

    [Fact]
    public void ImagesPerLocale_ShouldRespectStoreLimits()
    {
        MockupExportRules.ImagesPerLocale(Catalog.Find("google.screenshots.phone"), 10).Should().Be(8);
        MockupExportRules.ImagesPerLocale(Catalog.Find("apple.screenshots.ipad-13"), 10).Should().Be(10);
        MockupExportRules.ImagesPerLocale(Catalog.Find("google.graphics.feature-graphic"), 6).Should().Be(1);
    }

    [Theory]
    [InlineData("auto", "apple.screenshots.ipad-13", MockupFrameKind.Tablet)]
    [InlineData("none", "apple.screenshots.ipad-13", MockupFrameKind.None)]
    [InlineData("phone", "microsoft.screenshots.desktop", MockupFrameKind.Phone)]
    [InlineData("foldable", "apple.screenshots.iphone-duo-inner", MockupFrameKind.FoldableInner)]
    [InlineData("foldable", "apple.screenshots.iphone-dynamic-island-medium", MockupFrameKind.FoldableOuter)]
    [InlineData("phone", "google.graphics.feature-graphic", MockupFrameKind.None)]
    public void ResolveFrame_ShouldFollowTheClassUnlessForced(string kind, string classKey, MockupFrameKind expected)
    {
        MockupExportRules.ResolveFrame(Catalog.Find(classKey), kind).Should().Be(expected);
    }

    [Fact]
    public void PickSlotAsset_ShouldPreferLocaleThenFamilyThenPhone()
    {
        List<MockupSlot> slots =
        [
            new() { Order = 1, Family = MockupDeviceFamily.Phone, AssetId = 10 },
            new() { Order = 1, Family = MockupDeviceFamily.Phone, Locale = "tr", AssetId = 11 },
            new() { Order = 1, Family = MockupDeviceFamily.Tablet, AssetId = 12 },
            new() { Order = 2, Family = MockupDeviceFamily.Phone, AssetId = 20 },
        ];

        MockupExportRules.PickSlotAsset(slots, 1, MockupDeviceFamily.Phone, "tr").Slot.AssetId.Should().Be(11);
        MockupExportRules.PickSlotAsset(slots, 1, MockupDeviceFamily.Phone, "en-US").Slot.AssetId.Should().Be(10);
        MockupExportRules.PickSlotAsset(slots, 1, MockupDeviceFamily.Tablet, "tr").Should().Be((slots[2], false));
        MockupExportRules.PickSlotAsset(slots, 2, MockupDeviceFamily.Desktop, "tr").Should().Be((slots[3], true));
        MockupExportRules.PickSlotAsset(slots, 3, MockupDeviceFamily.Phone, "tr").Slot.Should().BeNull();
    }

    [Fact]
    public void PickCaption_EmptyTitle_ShouldFallBackToThePrimaryLocale()
    {
        List<MockupCaption> captions =
        [
            new() { Locale = "tr", SlotOrder = 5, Title = "Birlikte ilerle", Subtitle = "Hedefini paylaş" },
            new() { Locale = "de-DE", SlotOrder = 5, Title = null },
            new() { Locale = "en-US", SlotOrder = 5, Title = "Move forward together" },
        ];

        MockupExportRules.PickCaption(captions, "de-DE", "tr", 5).Should().Be(("Birlikte ilerle", "Hedefini paylaş", true));
        MockupExportRules.PickCaption(captions, "en-US", "tr", 5).Should().Be(("Move forward together", null, false));
        MockupExportRules.PickCaption(captions, "tr", "tr", 6).Should().Be(((string)null, (string)null, false));
    }

    [Fact]
    public void ZipPathAndFileName_ShouldBeStable()
    {
        MockupExportRules.ZipEntryPath(StoreKind.Apple, "apple.screenshots.ipad-13", "tr", 2).Should().Be("apple/ipad-13/tr/02.png");
        MockupExportRules.OutputFileName("google.screenshots.tablet-10", "en-US", 10).Should().Be("tablet-10-en-US-10.png");
    }

    [Fact]
    public void Plan_Default_ShouldSelectRequiredAndRecommendedOfTheAppsStores()
    {
        var (plan, error) = MockupExportPlanner.PlanForTemplate(Catalog, Template(), [StoreKind.Apple, StoreKind.Google], null, null, null, _today);

        error.Should().BeNull();
        plan.Items.Should().NotContain(i => i.Store == StoreKind.Microsoft);
        plan.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-dynamic-island-medium").Status.Should().Be(MockupRequirement.Required);
        plan.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-face-id-large").Selected.Should().BeFalse();
        plan.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-dynamic-island-large").Should().Match<Application.Dtos.MockupDtos.MockupExportPlanItemDto>(i => i.Selected && i.Status == MockupRequirement.Recommended && i.Width == 1320);
        plan.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-duo-outer").Selected.Should().BeTrue();
        plan.Items.Single(i => i.ClassKey == "google.graphics.feature-graphic").PerLocaleCount.Should().Be(1);

        // 3 locales x (5 Apple classes x 6 + Play phone 6 + two tablets 6 + feature graphic 1) = 3 x 49.
        plan.Total.Should().Be(3 * ((5 * 6) + 6 + 12 + 1));
        plan.Items.Where(i => i.Store == StoreKind.Apple).Select(i => i.Status).Should().BeInAscendingOrder();
    }

    [Fact]
    public void Plan_RefusedSize_ShouldBeListedAndTheDefaultUsed()
    {
        var targets = new List<Application.Dtos.StoreJobDtos.MockupExportTarget> { new("apple.screenshots.iphone-dynamic-island-medium", 1320, 2868), new("apple.screenshots.iphone-dynamic-island-medium", 1179, 2556) };
        var refused = new List<Application.Dtos.StoreJobDtos.MockupExportTarget> { new("apple.screenshots.iphone-dynamic-island-medium", 1320, 2868) };

        var (accepted, _) = MockupExportPlanner.PlanForTemplate(Catalog, Template(), [StoreKind.Apple], ["tr"], null, targets, _today);
        var (rejected, _) = MockupExportPlanner.PlanForTemplate(Catalog, Template(), [StoreKind.Apple], ["tr"], null, refused, _today);

        accepted.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-dynamic-island-medium").Width.Should().Be(1179);
        rejected.Rejected.Should().ContainSingle(r => r.Width == 1320 && r.Reason.Contains("1206x2622"));
        rejected.Items.Single(i => i.ClassKey == "apple.screenshots.iphone-dynamic-island-medium").Width.Should().Be(1206);
    }

    [Fact]
    public void Plan_ExplicitSelectionAndUnknownLocale()
    {
        var (plan, _) = MockupExportPlanner.PlanForTemplate(Catalog, Template(10), [], ["tr", "TR"], ["google.screenshots.phone"], null, _today);
        var (none, error) = MockupExportPlanner.PlanForTemplate(Catalog, Template(), [], ["fr"], null, null, _today);

        plan.Locales.Should().Equal("tr");
        plan.Items.Where(i => i.Selected).Select(i => i.ClassKey).Should().Equal("google.screenshots.phone");
        plan.Total.Should().Be(8);
        plan.Warnings.Should().ContainSingle(w => w.Code == "TooManySlots");
        none.Should().BeNull();
        error.Should().Be(Application.Utils.Constants.MessageKey.MockupLocaleInvalid);
    }

    [Fact]
    public void NarrowestCaptionClass_ShouldBeAPortraitClass()
    {
        MockupRenderPlanner.NarrowestCaptionClass(Catalog.Classes).ExportSize.IsLandscape.Should().BeFalse();
    }

    [Fact]
    public void RenderPlanner_FeatureGraphic_ShouldUseTheFirstCaptionAndNoScreenshot()
    {
        var template = Template(3, "en-US");

        template.Slots.Add(new MockupSlot { Order = 1, Family = MockupDeviceFamily.Phone, AssetId = 1 });
        template.Captions.Add(new MockupCaption { Locale = "en-US", SlotOrder = 1, Title = "Hello" });

        var (request, notes) = MockupRenderPlanner.Build(template, template.Spec, Catalog.Find("google.graphics.feature-graphic"), new PixelSize(1024, 500), "en-US", 1, new Dictionary<long, byte[]> { [1] = [1] }, 1, true);

        request.Screenshot.Should().BeNull();
        request.Frame.Should().Be(MockupFrameKind.None);
        request.Title.Should().Be("Hello");
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task AssetReader_ShouldCacheAndEvictLeastRecentlyUsed()
    {
        var storage = new CountingStorage();
        var reader = new StudioAssetReader(storage, 400);

        await reader.ReadAsync("a", CancellationToken.None);
        await reader.ReadAsync("a", CancellationToken.None);
        await reader.ReadAsync("b", CancellationToken.None);
        await reader.ReadAsync("c", CancellationToken.None);
        await reader.ReadAsync("d", CancellationToken.None);
        await reader.ReadAsync("e", CancellationToken.None);
        await reader.ReadAsync("a", CancellationToken.None);

        storage.Reads.Should().Equal("a", "b", "c", "d", "e", "a");
        reader.CachedBytes.Should().BeLessThanOrEqualTo(400);
    }

    private sealed class CountingStorage : Application.Interfaces.IFileStorage
    {
        public List<string> Reads { get; } = [];

        public bool IsConfigured => true;

        public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
        {
            Reads.Add(key);

            return Task.FromResult<Stream>(new MemoryStream(new byte[100]));
        }

        public Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public string CreateUploadUrl(string key, string contentType, TimeSpan lifetime) => throw new NotSupportedException();

        public string CreateDownloadUrl(string key, TimeSpan lifetime, string downloadFileName = null) => throw new NotSupportedException();

        public Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken cancellationToken) => throw new NotSupportedException();

        public string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan lifetime) => throw new NotSupportedException();

        public Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<Application.Interfaces.UploadedPart> parts, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Application.Interfaces.StoredObjectInfo> GetObjectInfoAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
