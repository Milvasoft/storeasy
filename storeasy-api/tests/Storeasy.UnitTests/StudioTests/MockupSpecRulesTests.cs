using FluentAssertions;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Studio;
using Storeasy.Infrastructure.Studio;
using System.Text.Json;

namespace Storeasy.UnitTests.StudioTests;

[Trait("Studio Unit Tests", "Template schema rules.")]
public class MockupSpecRulesTests
{
    [Fact]
    public void Default_ShouldBeValid()
    {
        MockupSpecRules.Validate(MockupSpecRules.CreateDefault()).Should().BeEmpty();
    }

    [Fact]
    public void Validate_Null_ShouldFail()
    {
        MockupSpecRules.Validate(null).Should().ContainSingle();
    }

    [Fact]
    public void Validate_ShouldReportEveryProblemWithItsPath()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.SchemaVersion = 7;
        spec.Background = new MockupBackgroundSpec { Kind = "plaid", Colors = ["red"], Angle = 400 };
        spec.Frame.Scale = 3;
        spec.Frame.Cutout = "teardrop";
        spec.Text.Font = "comic";
        spec.Text.Title.Weight = 650;
        spec.Text.Subtitle.MaxLines = 0;
        spec.Layers = [new MockupLayerSpec { Type = "badge", Color = "#FFFFFF", TextColor = "#000000", TextSize = 0.04f, Texts = [] }];

        var errors = MockupSpecRules.Validate(spec);

        errors.Should().Contain(e => e.StartsWith("schemaVersion"))
                       .And.Contain(e => e.StartsWith("background.kind"))
                       .And.Contain(e => e.StartsWith("background.colors[0]"))
                       .And.Contain(e => e.StartsWith("background.angle"))
                       .And.Contain(e => e.StartsWith("frame.scale"))
                       .And.Contain(e => e.StartsWith("frame.cutout"))
                       .And.Contain(e => e.StartsWith("text.font"))
                       .And.Contain(e => e.StartsWith("text.title.weight"))
                       .And.Contain(e => e.StartsWith("text.subtitle.maxLines"))
                       .And.Contain(e => e.StartsWith("layers[0].texts"));
    }

    [Fact]
    public void Validate_GradientWithOneColor_AndImageWithoutAsset_ShouldFail()
    {
        var gradient = MockupSpecRules.CreateDefault();
        var image = MockupSpecRules.CreateDefault();

        gradient.Background.Colors = ["#112233"];
        image.Background = new MockupBackgroundSpec { Kind = MockupSpecRules.BackgroundKinds.Image, Colors = ["#112233"] };

        MockupSpecRules.Validate(gradient).Should().Contain(e => e.StartsWith("background.colors"));
        MockupSpecRules.Validate(image).Should().Contain(e => e.StartsWith("background.assetId"));
    }

    [Fact]
    public void Validate_UnknownCanvasClass_ShouldFail()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Canvas.ClassKey = "apple.screenshots.unknown";

        MockupSpecRules.Validate(spec).Should().Contain(e => e.StartsWith("canvas.classKey"));
    }

    [Fact]
    public void Upgrade_ShouldFillMissingSectionsAndVersion()
    {
        var spec = MockupSpecRules.Upgrade(new MockupSpec { SchemaVersion = 0 });

        spec.SchemaVersion.Should().Be(MockupSpecRules.CurrentSchemaVersion);
        MockupSpecRules.Validate(spec).Should().BeEmpty();
    }

    [Fact]
    public void Spec_ShouldRoundTripThroughJson()
    {
        var spec = MockupSpecRules.CreateDefault();

        spec.Layers = [new MockupLayerSpec { Type = "badge", X = 0.8f, Y = 0.3f, Color = "#FFD25A", TextColor = "#1B1464", TextSize = 0.04f, Texts = new() { ["*"] = "NEW", ["tr"] = "YENİ" } }];

        var copy = JsonSerializer.Deserialize<MockupSpec>(JsonSerializer.Serialize(spec));

        JsonSerializer.Serialize(copy).Should().Be(JsonSerializer.Serialize(spec));
    }

    [Theory]
    [InlineData("tr", true)]
    [InlineData("en-US", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("EN", false)]
    [InlineData("en_US", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocale_ShouldAcceptStoreLanguageTags(string locale, bool expected)
    {
        MockupSpecRules.IsLocale(locale).Should().Be(expected);
    }

    [Fact]
    public void StarterTemplates_ShouldBeValidOriginalDesignsWithEnglishAndTurkishSamples()
    {
        var catalog = StarterTemplateCatalog.LoadEmbedded();

        catalog.All.Should().HaveCountGreaterThanOrEqualTo(3);
        catalog.All.Select(s => s.Key).Should().OnlyHaveUniqueItems();
        catalog.Find("ultramarine").Should().NotBeNull();
        catalog.Find("nope").Should().BeNull();

        foreach (var starter in catalog.All)
        {
            MockupSpecRules.Validate(starter.Spec).Should().BeEmpty(starter.Key);
            starter.Captions.Keys.Should().Contain(["en", "tr"]);
            starter.Captions.Values.Should().AllSatisfy(samples => samples.Should().HaveCount(starter.SlotCount));
        }
    }
}
