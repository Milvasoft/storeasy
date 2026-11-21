using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores.Content;
using Storeasy.Infrastructure.Stores.Experiments;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.InfrastructureTests;

/// <summary>
/// The App Store Connect experiment and custom product page bodies storeasy sends, and how it reads Apple's answers
/// (fixtures shaped as the App Store Connect API reference documents them).
/// </summary>
[Trait("Infrastructure Unit Tests", "App Store experiment payloads.")]
public class AppleExperimentPayloadsTests
{
    [Fact]
    public void CreateExperiment_ShouldUseTheV2ShapeWithTheAppRelationship()
    {
        var body = AppleExperimentPayloads.CreateExperiment("1234567890", "Icon test", "IOS", 30);

        Same(body, """
        {"data":{"type":"appStoreVersionExperiments","attributes":{"name":"Icon test","platform":"IOS","trafficProportion":30},
          "relationships":{"app":{"data":{"type":"apps","id":"1234567890"}}}}}
        """);
    }

    [Fact]
    public void UpdateExperiment_ShouldStartAndStopThroughStarted()
    {
        Same(AppleExperimentPayloads.UpdateExperiment("e1", started: true), """{"data":{"type":"appStoreVersionExperiments","id":"e1","attributes":{"started":true}}}""");
        Same(AppleExperimentPayloads.UpdateExperiment("e1", "Renamed", 40), """{"data":{"type":"appStoreVersionExperiments","id":"e1","attributes":{"name":"Renamed","trafficProportion":40}}}""");
    }

    [Fact]
    public void Treatments_ShouldHangUnderTheV2ExperimentAndCarryTheIconName()
    {
        Same(AppleExperimentPayloads.CreateTreatment("e1", "Dark icon", "AppIconDark"), """
        {"data":{"type":"appStoreVersionExperimentTreatments","attributes":{"name":"Dark icon","appIconName":"AppIconDark"},
          "relationships":{"appStoreVersionExperimentV2":{"data":{"type":"appStoreVersionExperiments","id":"e1"}}}}}
        """);

        Same(AppleExperimentPayloads.CreateTreatmentLocalization("t1", "tr"), """
        {"data":{"type":"appStoreVersionExperimentTreatmentLocalizations","attributes":{"locale":"tr"},
          "relationships":{"appStoreVersionExperimentTreatment":{"data":{"type":"appStoreVersionExperimentTreatments","id":"t1"}}}}}
        """);
    }

    [Fact]
    public void CreatePage_ShouldCreateTheFirstVersionAndLocalizationInline()
    {
        Same(AppleExperimentPayloads.CreatePage("1234567890", "Students", "en-US", "Study smarter."), """
        {"data":{"type":"appCustomProductPages","attributes":{"name":"Students"},
          "relationships":{"app":{"data":{"type":"apps","id":"1234567890"}},
            "appCustomProductPageVersions":{"data":[{"type":"appCustomProductPageVersions","id":"${new-version-1}"}]}}},
         "included":[
          {"type":"appCustomProductPageVersions","id":"${new-version-1}",
           "relationships":{"appCustomProductPageLocalizations":{"data":[{"type":"appCustomProductPageLocalizations","id":"${new-localization-1}"}]}}},
          {"type":"appCustomProductPageLocalizations","id":"${new-localization-1}","attributes":{"locale":"en-US","promotionalText":"Study smarter."}}]}
        """);
    }

    [Fact]
    public void SubmissionItem_ShouldReferTheExperimentOrThePageVersion()
    {
        Same(AppleExperimentPayloads.SubmissionItem("s1", AsoExperimentKind.ProductPageOptimization, "e1"), """
        {"data":{"type":"reviewSubmissionItems","relationships":{"reviewSubmission":{"data":{"type":"reviewSubmissions","id":"s1"}},
          "appStoreVersionExperimentV2":{"data":{"type":"appStoreVersionExperiments","id":"e1"}}}}}
        """);

        Same(AppleExperimentPayloads.SubmissionItem("s1", AsoExperimentKind.CustomProductPage, "v3"), """
        {"data":{"type":"reviewSubmissionItems","relationships":{"reviewSubmission":{"data":{"type":"reviewSubmissions","id":"s1"}},
          "appCustomProductPageVersion":{"data":{"type":"appCustomProductPageVersions","id":"v3"}}}}}
        """);
    }

    [Fact]
    public void ParseExperiment_ShouldReadStateDatesAndTraffic()
    {
        using var document = JsonDocument.Parse(Fixture("asc-experiments-v2.json"));
        var experiments = JsonRead.Items(document.RootElement, "data").Select(AppleExperimentPayloads.ParseExperiment).ToList();

        experiments.Should().HaveCount(2);
        experiments[0].Name.Should().Be("Icon test");
        experiments[0].State.Should().Be("APPROVED");
        experiments[0].TrafficProportion.Should().Be(30);
        experiments[0].ReviewRequired.Should().BeTrue();
        experiments[0].StartDate.Should().Be(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc));
        experiments[0].EndDate.Should().BeNull();
        experiments[1].ReviewRequired.Should().BeFalse();
        experiments[1].StartDate.Should().BeNull();
    }

    [Fact]
    public void ParseTreatment_ShouldReadTheIconAndPromotion()
    {
        using var document = JsonDocument.Parse(Fixture("asc-experiment-treatments.json"));
        var treatments = JsonRead.Items(document.RootElement, "data").Select(AppleExperimentPayloads.ParseTreatment).ToList();

        treatments.Select(t => t.AppIconName).Should().Equal("AppIconDark", null);
        treatments[0].PromotedDate.Should().Be(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParsePage_ShouldReadTheAddressAndPickTheVersionBeingEdited()
    {
        using var pages = JsonDocument.Parse(Fixture("asc-custom-product-pages.json"));
        using var versions = JsonDocument.Parse(Fixture("asc-custom-product-page-versions.json"));
        using var localizations = JsonDocument.Parse(Fixture("asc-custom-product-page-localizations.json"));

        var page = AppleExperimentPayloads.ParsePage(JsonRead.Items(pages.RootElement, "data").Single());
        var latest = AppleExperimentPayloads.LatestVersion([.. JsonRead.Items(versions.RootElement, "data")]);
        var locales = JsonRead.Items(localizations.RootElement, "data").Select(AppleExperimentPayloads.ParsePageLocalization).ToList();

        page.Url.Should().EndWith("?ppid=8d2b7a10-aaaa-4bbb-8ccc-0dddeeefff00");
        page.Visible.Should().BeTrue();
        JsonRead.Str(latest, "id").Should().Be("v-3");
        locales.Select(l => (l.Locale, l.PromotionalText)).Should().Equal(("en-US", "Study smarter."), ("tr", null));
    }

    private static void Same(JsonObject actual, string expected)
        => JsonNode.DeepEquals(actual, JsonNode.Parse(expected)).Should().BeTrue($"{actual.ToJsonString()} should equal {expected}");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfrastructureTests", "Fixtures", name));
}
