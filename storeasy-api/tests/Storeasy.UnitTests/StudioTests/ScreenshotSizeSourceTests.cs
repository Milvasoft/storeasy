using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Studio;
using Storeasy.Infrastructure.Studio;

namespace Storeasy.UnitTests.StudioTests;

[Trait("Studio Unit Tests", "Screenshot sizes from the rules catalog.")]
public class ScreenshotSizeSourceTests
{
    private static ScreenshotSizeCatalog Catalog => StudioTestImages.Catalog;

    [Theory]
    [InlineData("apple.screenshots.iphone-dynamic-island-medium", StoreKind.Apple, MockupFrameKind.Phone, 1206, 2622)]
    [InlineData("apple.screenshots.iphone-dynamic-island-large", StoreKind.Apple, MockupFrameKind.Phone, 1320, 2868)]
    [InlineData("apple.screenshots.ipad-13", StoreKind.Apple, MockupFrameKind.Tablet, 2064, 2752)]
    [InlineData("apple.screenshots.iphone-duo-outer", StoreKind.Apple, MockupFrameKind.FoldableOuter, 1398, 2034)]
    [InlineData("apple.screenshots.iphone-duo-inner", StoreKind.Apple, MockupFrameKind.FoldableInner, 2007, 2853)]
    [InlineData("google.screenshots.phone", StoreKind.Google, MockupFrameKind.Phone, 1080, 1920)]
    [InlineData("google.screenshots.tablet-7", StoreKind.Google, MockupFrameKind.Tablet, 1080, 1920)]
    [InlineData("google.screenshots.tablet-10", StoreKind.Google, MockupFrameKind.Tablet, 1440, 2560)]
    [InlineData("google.graphics.feature-graphic", StoreKind.Google, MockupFrameKind.None, 1024, 500)]
    [InlineData("microsoft.screenshots.desktop", StoreKind.Microsoft, MockupFrameKind.Desktop, 1920, 1080)]
    public void Load_ShouldReadEveryStudioClassFromTheCatalog(string key, StoreKind store, MockupFrameKind frame, int width, int height)
    {
        var screenshotClass = Catalog.Find(key);

        screenshotClass.Should().NotBeNull();
        screenshotClass.Store.Should().Be(store);
        screenshotClass.Frame.Should().Be(frame);
        screenshotClass.ExportSize.Should().Be(new PixelSize(width, height));
        MockupExportRules.Accepts(screenshotClass, screenshotClass.ExportSize).Should().BeTrue("the export size is one the store accepts");
        screenshotClass.LabelEn.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Load_ShouldCarryVersionCountsAndLevels()
    {
        Catalog.CatalogVersion.Should().NotBeNullOrWhiteSpace();
        Catalog.Classes.Should().HaveCount(StudioClassMap.Definitions.Count);

        Catalog.Find("apple.screenshots.iphone-dynamic-island-medium").MaxCount.Should().Be(10);
        Catalog.Find("google.screenshots.phone").MaxCount.Should().Be(8);
        Catalog.Find("google.screenshots.phone").Level.Should().Be("required", "the general Play rule requires screenshots");
        Catalog.Find("microsoft.screenshots.desktop").Level.Should().Be("required");
        Catalog.Find("apple.screenshots.iphone-duo-inner").Level.Should().Be("required-from");
        Catalog.Find("apple.screenshots.iphone-duo-inner").RequiredFrom.Should().Be(new DateOnly(2027, 4, 1));
        Catalog.Find("apple.screenshots.iphone-face-id-large").SatisfiedBy.Should().Contain("apple.screenshots.iphone-dynamic-island-large");
        Catalog.Find("google.graphics.feature-graphic").SingleImage.Should().BeTrue();
    }

    [Fact]
    public void Build_MissingRule_ShouldFailLoudly()
    {
        var empty = new Domain.Rules.RuleCatalog("0.0.1", DateTime.UtcNow, [], "test");

        var build = () => RulesCatalogScreenshotSizeSource.Build(empty);

        build.Should().Throw<InvalidOperationException>().WithMessage("*apple.screenshots.iphone-dynamic-island-medium*");
    }

    [Fact]
    public void Source_ShouldReadTheApiRulesCatalogLazily()
    {
        var catalog = new Moq.Mock<Application.Interfaces.IRulesCatalog>();

        catalog.SetupGet(c => c.Current).Returns(Infrastructure.Services.RulesCatalog.Load(null).Current);

        var source = new RulesCatalogScreenshotSizeSource(catalog.Object);

        catalog.VerifyGet(c => c.Current, Moq.Times.Never());
        source.Current.Find("apple.screenshots.ipad-13").ExportSize.Should().Be(new PixelSize(2064, 2752));
        _ = source.Current;
        catalog.VerifyGet(c => c.Current, Moq.Times.Once());
    }
}
