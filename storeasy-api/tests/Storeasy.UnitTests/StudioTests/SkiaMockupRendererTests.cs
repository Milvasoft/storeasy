using FluentAssertions;
using SkiaSharp;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Studio;
using Storeasy.Infrastructure.Studio.Rendering;

namespace Storeasy.UnitTests.StudioTests;

[Trait("Studio Unit Tests", "SkiaSharp mockup renderer.")]
public class SkiaMockupRendererTests
{
    private const float _goldenScale = 0.2f;

    private static MockupRenderRequest Request(PixelSize size, MockupFrameKind frame, string locale = "en-US", string title = "Plan your day at a glance", string subtitle = "Everything that matters on one calm screen", MockupSpec spec = null, byte[] screenshot = null, float scale = 1)
        => new()
        {
            Spec = spec ?? MockupSpecRules.CreateDefault(),
            Size = size,
            Frame = frame,
            Locale = locale,
            Title = title,
            Subtitle = subtitle,
            Screenshot = screenshot,
            OutputScale = scale,
        };

    public static TheoryData<string> StudioClassKeys => [.. StudioClassMap.Definitions.Select(d => d.RuleId)];

    [Theory]
    [MemberData(nameof(StudioClassKeys))]
    public void Render_ShouldProduceTheExactCatalogSizeWithoutAlpha(string classKey)
    {
        // Arrange
        var screenshotClass = StudioTestImages.Catalog.Find(classKey);
        var raw = StudioTestImages.Screenshot(screenshotClass.ExportSize.Width, screenshotClass.ExportSize.Height);

        // Act
        var result = StudioTestImages.Renderer.Render(Request(screenshotClass.ExportSize, screenshotClass.Frame, screenshot: screenshotClass.SingleImage ? null : raw));

        // Assert
        result.Failure.Should().BeNull();
        StudioTestImages.PngSize(result.Png).Should().Be((screenshotClass.ExportSize.Width, screenshotClass.ExportSize.Height));
        StudioTestImages.PngColorType(result.Png).Should().Be(2, "store screenshots must not carry an alpha channel");
    }

    [Fact]
    public void Render_ShouldBeDeterministic()
    {
        var size = new PixelSize(1206, 2622);
        var raw = StudioTestImages.Screenshot(1206, 2622);

        var first = StudioTestImages.Renderer.Render(Request(size, MockupFrameKind.Phone, screenshot: raw, scale: 0.3f));
        var second = StudioTestImages.Renderer.Render(Request(size, MockupFrameKind.Phone, screenshot: raw, scale: 0.3f));

        first.Png.Should().Equal(second.Png);
    }

    [Fact]
    public void Render_PreviewScale_ShouldKeepTheLayoutAndShrinkTheImage()
    {
        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(2064, 2752), MockupFrameKind.Tablet, scale: 0.25f));

        StudioTestImages.PngSize(result.Png).Should().Be((516, 688));
    }

    [Fact]
    public void Render_TransparentScreenshot_ShouldBeFlattenedWithAWarning()
    {
        var raw = StudioTestImages.Screenshot(603, 1311, alpha: 128);

        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(603, 1311), MockupFrameKind.Phone, screenshot: raw));

        result.Warnings.Select(w => w.Code).Should().Contain(MockupWarningCodes.AlphaFlattened);
        StudioTestImages.PngColorType(result.Png).Should().Be(2);
    }

    [Fact]
    public void Render_TooSmallScreenshot_ShouldFailForExportAndWarnForPreview()
    {
        var raw = StudioTestImages.Screenshot(300, 652);
        var request = Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, screenshot: raw);

        var export = StudioTestImages.Renderer.Render(request);
        var preview = StudioTestImages.Renderer.Render(request with { FailWhenTooSmall = false, OutputScale = 0.2f });

        export.Png.Should().BeNull();
        export.Failure.Code.Should().Be(MockupWarningCodes.AssetTooSmall);
        preview.Png.Should().NotBeNull();
        preview.Warnings.Select(w => w.Code).Should().Contain(MockupWarningCodes.AssetTooSmall);
    }

    [Fact]
    public void Render_SlightlyUpscaledScreenshot_ShouldWarn()
    {
        var raw = StudioTestImages.Screenshot(800, 1739);

        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, screenshot: raw));

        result.Failure.Should().BeNull();
        result.Warnings.Select(w => w.Code).Should().Contain(MockupWarningCodes.AssetUpscaled);
    }

    [Fact]
    public void Render_PhoneScreenshotInADesktopWindow_ShouldBeFittedInside()
    {
        var raw = StudioTestImages.Screenshot(1206, 2622);

        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(1920, 1080), MockupFrameKind.Desktop, screenshot: raw));

        result.Failure.Should().BeNull();
        result.Warnings.Select(w => w.Code).Should().Contain(MockupWarningCodes.AspectMismatch);
    }

    [Fact]
    public void Render_UnreadableScreenshot_ShouldFail()
    {
        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, screenshot: [1, 2, 3, 4]));

        result.Failure.Code.Should().Be(MockupWarningCodes.AssetUnreadable);
    }

    [Fact]
    public void Render_MissingScreenshotAndBackgroundImage_ShouldWarnAndStillDraw()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Background = new MockupBackgroundSpec { Kind = MockupSpecRules.BackgroundKinds.Image, Colors = ["#203040"], AssetId = 5 };

        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, spec: spec, scale: 0.2f));

        result.Png.Should().NotBeNull();
        result.Warnings.Select(w => w.Code).Should().Contain([MockupWarningCodes.ScreenshotMissing, MockupWarningCodes.BackgroundMissing]);
    }

    [Fact]
    public void Render_HeavierWeightThanEmbedded_ShouldWarnFontMissing()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Text.Title.Weight = 900;

        var result = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, spec: spec, scale: 0.2f));

        result.Warnings.Should().ContainSingle(w => w.Code == MockupWarningCodes.FontMissing && w.Field == "title");
    }

    [Fact]
    public void MeasureCaptions_LongTitle_ShouldShrinkAndThenOverflow()
    {
        var spec = MockupSpecRules.CreateDefault();
        var size = new PixelSize(1206, 2622);

        var shortTitle = StudioTestImages.Renderer.MeasureCaptions(spec, size, MockupFrameKind.Phone, "de-DE", "Fortschritt sehen", null);
        var longTitle = StudioTestImages.Renderer.MeasureCaptions(spec, size, MockupFrameKind.Phone, "de-DE", "Jeden einzelnen Schritt abhaken und festhalten, damit du jeden Tag siehst, wie weit du schon gekommen bist", null);

        shortTitle.TitleLines.Should().Be(1);
        shortTitle.Warnings.Should().BeEmpty();
        longTitle.TitleSize.Should().BeLessThan(shortTitle.TitleSize, "auto fit shrinks before it overflows");
        longTitle.TitleLines.Should().BeGreaterThan(2);
        longTitle.Warnings.Should().ContainSingle(w => w.Code == MockupWarningCodes.CaptionOverflow && w.Lines == longTitle.TitleLines && w.MaxLines == 2);
    }

    [Fact]
    public void MeasureCaptions_WithoutAutoFit_ShouldKeepTheSize()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Text.Title.AutoFit = false;

        var measure = StudioTestImages.Renderer.MeasureCaptions(spec, new PixelSize(1206, 2622), MockupFrameKind.Phone, "en", "A rather long title that will need several lines to fit", null);

        measure.TitleSize.Should().BeApproximately(spec.Text.Title.Size * 1206, 0.01f);
    }

    [Fact]
    public void MeasureCaptions_Japanese_ShouldBreakBetweenCharacters()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Text.Title.AutoFit = false;
        spec.Text.Title.MaxLines = 6;

        var measure = StudioTestImages.Renderer.MeasureCaptions(spec, new PixelSize(1206, 2622), MockupFrameKind.Phone, "ja", "毎日の目標を一目で確認して、小さな一歩を積み重ねましょう", null);

        measure.TitleLines.Should().BeGreaterThan(1, "Japanese has no spaces and breaks between characters");
    }

    [Fact]
    public void TurkishUppercase_ShouldUseTheDottedCapitalI()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Text.Uppercase = true;

        var turkish = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, "tr", "bilgi", null, spec, scale: 0.2f));
        var english = StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, "en", "bilgi", null, spec, scale: 0.2f));

        MockupSpecRules.Culture("tr").TextInfo.ToUpper("bilgi").Should().Be("BİLGİ");
        turkish.Png.Should().NotEqual(english.Png, "the Turkish I carries a dot");
    }

    [Theory]
    [InlineData("مرحبا بك", true)]
    [InlineData("שלום", true)]
    [InlineData("123 שלום", true)]
    [InlineData("Merhaba", false)]
    [InlineData("storeasy مرحبا", false)]
    public void IsRightToLeft_ShouldFollowTheFirstStrongCharacter(string text, bool expected)
    {
        CaptionTypesetter.IsRightToLeft(text).Should().Be(expected);
    }

    [Fact]
    public void FontLibrary_ShouldShipEveryFontWithItsLicense()
    {
        foreach (var file in StudioFontLibrary.AllFiles())
            File.Exists(Path.Combine(StudioTestImages.Fonts.Folder, file)).Should().BeTrue(file);

        foreach (var folder in new[] { "Inter", "NotoSans", "NotoSansCJK" })
            File.ReadAllText(Path.Combine(StudioTestImages.Fonts.Folder, folder, "OFL.txt")).Should().Contain("SIL Open Font License");
    }

    [Theory]
    [InlineData("inter", 900, 800)]
    [InlineData("inter", 600, 600)]
    [InlineData("noto-sans-arabic", 600, 700)]
    [InlineData("noto-sans-jp", 300, 400)]
    public void NearestWeight_ShouldPickTheClosestEmbeddedWeight(string family, int requested, int expected)
    {
        StudioFontLibrary.NearestWeight(family, requested).Should().Be(expected);
    }

    [Fact]
    public void FontLibrary_ShouldCoverTurkishArabicHebrewThaiDevanagariAndCjk()
    {
        var chain = StudioFontLibrary.FallbackChain("inter", "ja").Select(f => StudioTestImages.Fonts.Get(f, 400)).ToList();

        foreach (var codePoint in new[] { 0x011F, 0x0131, 0x0130, 0x015F, 0x0645, 0x05E9, 0x0E01, 0x0915, 0x65E5, 0x3042, 0xAC00 })
            chain.Any(t => t.ContainsGlyph(codePoint)).Should().BeTrue($"U+{codePoint:X4} is covered");
    }

    public static TheoryData<string, string, MockupFrameKind, int, int, string, string> Goldens => new()
    {
        { "phone-island-en", "en-US", MockupFrameKind.Phone, 1206, 2622, "Plan your day at a glance", "Everything that matters on one calm screen" },
        { "phone-tr", "tr", MockupFrameKind.Phone, 1206, 2622, "Gününü tek bakışta planla", "Önemli olan her şey tek ekranda" },
        { "tablet-de", "de-DE", MockupFrameKind.Tablet, 2064, 2752, "Deine Ziele auf einen Blick", "Deine ganze Route auf einem Screen" },
        { "foldable-outer-en", "en-US", MockupFrameKind.FoldableOuter, 1398, 2034, "See how far you came", "Weekly charts that draw themselves" },
        { "foldable-inner-ja", "ja", MockupFrameKind.FoldableInner, 2007, 2853, "一目でわかる毎日の予定", "大切なことをひとつの画面に" },
        { "desktop-ar", "ar", MockupFrameKind.Desktop, 1920, 1080, "خطط يومك بنظرة واحدة", "كل ما يهم في شاشة واحدة" },
        { "feature-ko", "ko", MockupFrameKind.None, 1024, 500, "하루를 한눈에 계획하세요", "중요한 것을 한 화면에" },
    };

    [Theory]
    [MemberData(nameof(Goldens))]
    public void Render_ShouldMatchGoldenImage(string name, string locale, MockupFrameKind frame, int width, int height, string title, string subtitle)
    {
        // Arrange
        var size = new PixelSize(width, height);
        var screenshotSize = frame == MockupFrameKind.Desktop ? new PixelSize(1920, 1080) : size;
        var raw = frame == MockupFrameKind.None ? null : StudioTestImages.Screenshot(screenshotSize.Width, screenshotSize.Height);

        // Act
        var result = StudioTestImages.Renderer.Render(Request(size, frame, locale, title, subtitle, screenshot: raw, scale: _goldenScale));

        // Assert
        result.Failure.Should().BeNull();
        StudioTestImages.ShouldMatchGolden(result.Png, name);
    }

    [Fact]
    public void Render_StarterTemplates_ShouldMatchGoldenImages()
    {
        var raw = StudioTestImages.Screenshot(1206, 2622);
        var catalog = Infrastructure.Studio.StarterTemplateCatalog.LoadEmbedded();

        foreach (var starter in catalog.All)
        {
            var caption = starter.Captions["tr"][0];
            var request = Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, "tr", caption.Title, caption.Subtitle, starter.Spec, raw, _goldenScale);

            var result = StudioTestImages.Renderer.Render(request);

            result.Failure.Should().BeNull();
            result.Warnings.Should().NotContain(w => w.Code == MockupWarningCodes.CaptionOverflow, $"the {starter.Key} sample captions fit");
            StudioTestImages.ShouldMatchGolden(result.Png, $"starter-{starter.Key}");
        }
    }

    [Fact]
    public void Render_DifferentFrames_ShouldDrawDifferentImages()
    {
        var size = new PixelSize(1206, 2622);
        var raw = StudioTestImages.Screenshot(1206, 2622);
        var images = Enum.GetValues<MockupFrameKind>().Select(kind => StudioTestImages.Renderer.Render(Request(size, kind, screenshot: raw, scale: 0.1f)).Png).ToList();

        images.Select(Convert.ToBase64String).Distinct().Should().HaveCount(images.Count);
    }

    [Fact]
    public void Render_Cutouts_ShouldDrawDifferentImages()
    {
        var images = MockupSpecRules.Cutouts.All.Select(cutout =>
        {
            var spec = MockupSpecRules.CreateDefault();

            spec.Frame.Cutout = cutout;

            using var bitmap = SKBitmap.Decode(StudioTestImages.Renderer.Render(Request(new PixelSize(1206, 2622), MockupFrameKind.Phone, spec: spec, scale: 0.3f)).Png);

            return Convert.ToBase64String(bitmap.Bytes);
        }).ToList();

        images.Distinct().Should().HaveCount(MockupSpecRules.Cutouts.All.Length);
    }
}
