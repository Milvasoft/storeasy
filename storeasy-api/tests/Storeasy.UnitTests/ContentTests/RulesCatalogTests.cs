using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Rules;
using Storeasy.Infrastructure.Services;
using Storeasy.UnitTests.TestSupport;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.ContentTests;

/// <summary>
/// <see cref="RulesCatalog"/> loading and validation, and <see cref="RuleEvaluator"/> / <see cref="MediaSlotEvaluator"/> on the
/// real catalog of the repository.
/// </summary>
[Trait("Content Unit Tests", "Rules catalog loading and evaluation.")]
public class RulesCatalogTests
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() => RulesCatalog.LoadFromFolder(RulesFolder()));

    private static string RulesFolder() => Path.GetFullPath(Path.Combine(SourceCode.SolutionRoot, "..", "rules"));

    private static RuleCatalog Catalog => _catalog.Value;

    private static readonly DateOnly _today = new(2026, 11, 18);

    [Fact]
    public void LoadFromFolder_ShouldLoadEveryStoreFileOfTheRepositoryCatalog()
    {
        var manifestVersion = JsonNode.Parse(File.ReadAllText(Path.Combine(RulesFolder(), "catalog.json")))!["catalogVersion"]!.GetValue<string>();

        Catalog.Version.Should().Be(manifestVersion);
        Catalog.Rules.Select(r => r.StoreKind).Distinct().Should().BeEquivalentTo([StoreKind.Apple, StoreKind.Google, StoreKind.Microsoft, StoreKind.Winget, StoreKind.Firefox]);
        Catalog.Find("apple.screenshots.ipad-13").Params.Sizes.Should().Contain(s => s.Width == 2064 && s.Height == 2752);
        Catalog.Find("apple.listing-limits.keywords").Params.MaxBytes.Should().Be(100);
    }

    [Fact]
    public void LoadEmbedded_ShouldMatchTheFolderCopy()
    {
        var embedded = RulesCatalog.LoadEmbedded();

        embedded.Version.Should().Be(Catalog.Version);
        embedded.Rules.Should().HaveCount(Catalog.Rules.Count);
        embedded.Source.Should().Be("embedded");
    }

    [Fact]
    public void Build_ShouldRefuseAnotherMajorVersion()
    {
        var manifest = ManifestWith("2.0.0");

        var act = () => RulesCatalog.Build(manifest, ReadFromRepository, "test");

        act.Should().Throw<InvalidOperationException>().WithMessage("*not supported*");
    }

    [Fact]
    public void Build_ShouldRefuseStoreFilesOfAnotherVersion()
    {
        var manifest = ManifestWith("1.0.2");

        var act = () => RulesCatalog.Build(manifest, ReadFromRepository, "test");

        act.Should().Throw<InvalidOperationException>().WithMessage("*differs from the manifest*");
    }

    [Fact]
    public void Build_ShouldRefuseUnknownRuleReferences()
    {
        var apple = JsonNode.Parse(ReadFromRepository("apple.json"));
        apple["rules"][1]["requirement"]["fallbackTo"] = new JsonArray("apple.screenshots.nothing");

        var act = () => RulesCatalog.Build(File.ReadAllText(Path.Combine(RulesFolder(), "catalog.json")), path => path == "apple.json" ? apple.ToJsonString() : ReadFromRepository(path), "test");

        act.Should().Throw<InvalidOperationException>().WithMessage("*unknown rule apple.screenshots.nothing*");
    }

    [Theory]
    [InlineData(new[] { "iphone" }, RuleApplicability.DoesNotApply)]
    [InlineData(new[] { "iphone", "ipad" }, RuleApplicability.Applies)]
    [InlineData(null, RuleApplicability.Unknown)]
    public void Evaluate_ShouldApplyTheIpadSetOnlyToAppsThatSupportIpad(string[] families, RuleApplicability expected)
    {
        var match = RuleEvaluator.Evaluate(Catalog.Find("apple.screenshots.ipad-13"), new RuleSubject { Store = StoreKind.Apple, Platforms = ["ios"], DeviceFamilies = families });

        match.Applicability.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, RuleApplicability.Unknown)]
    [InlineData("26.1", RuleApplicability.DoesNotApply)]
    [InlineData("27.1", RuleApplicability.Applies)]
    [InlineData("27.2.1", RuleApplicability.Applies)]
    public void Evaluate_ShouldReadTheBuildSdkOfTheDuoRule(string sdk, RuleApplicability expected)
    {
        var match = RuleEvaluator.Evaluate(Catalog.Find("apple.screenshots.iphone-duo-outer"), new RuleSubject
        {
            Store = StoreKind.Apple,
            Platforms = ["ios"],
            DeviceFamilies = ["iphone"],
            BuildSdkName = "iOS",
            BuildSdkVersion = sdk,
        });

        match.Applicability.Should().Be(expected);
    }

    [Fact]
    public void Evaluate_ShouldNeverApplyARuleOfAnotherStore()
    {
        RuleEvaluator.Evaluate(Catalog.Find("apple.listing-limits.name"), new RuleSubject { Store = StoreKind.Google }).Applicability.Should().Be(RuleApplicability.DoesNotApply);
    }

    [Fact]
    public void Timing_ShouldFollowTheDates()
    {
        var duo = Catalog.Find("apple.screenshots.iphone-duo-outer");
        var ageRating = Catalog.Find("apple.age-rating.shared-location-dating");

        RuleEvaluator.StatusOn(duo, _today).Should().Be(RuleStatus.Upcoming);
        RuleEvaluator.IsRequiredOn(duo, _today).Should().BeFalse();
        RuleEvaluator.IsRequiredOn(duo, new DateOnly(2027, 4, 1)).Should().BeTrue();
        RuleEvaluator.DueDate(ageRating).Should().Be(new DateOnly(2027, 1, 12));
        RuleEvaluator.StatusOn(ageRating, _today).Should().Be(RuleStatus.InForce);
    }

    [Theory]
    [InlineData("27.1", "27.1", true)]
    [InlineData("27.0.9", "27.1", false)]
    [InlineData("28", "27.1", true)]
    [InlineData("x", "27.1", false)]
    [InlineData("1.0", null, true)]
    public void VersionAtLeast_ShouldCompareNumerically(string version, string minimum, bool expected)
        => RuleEvaluator.VersionAtLeast(version, minimum).Should().Be(expected);

    [Fact]
    public void MediaSlots_ShouldFillTheRequiredIphoneClassesFromTheLargestSet()
    {
        var rules = Catalog.ForStore(StoreKind.Apple).Where(r => r.Category == "screenshots" && r.Id.Contains("iphone") && !r.Id.Contains("duo")).ToList();
        var counts = new Dictionary<string, int> { ["apple.screenshots.iphone-dynamic-island-large"] = 3 };

        var slots = MediaSlotEvaluator.Evaluate(rules, counts, _today).ToDictionary(s => s.RuleId);

        slots["apple.screenshots.iphone-dynamic-island-large"].Fill.Should().Be(MediaSlotFill.Own);
        slots["apple.screenshots.iphone-face-id-large"].Should().Match<MediaSlot>(s => s.Required && s.Fill == MediaSlotFill.Fallback && s.FallbackFromRuleId == "apple.screenshots.iphone-dynamic-island-large");
        slots["apple.screenshots.iphone-dynamic-island-medium"].Should().Match<MediaSlot>(s => s.Required && s.Fill == MediaSlotFill.Fallback);
    }

    [Fact]
    public void MediaSlots_WithoutAnyScreenshot_ShouldLeaveTheRequiredClassesEmpty()
    {
        var rules = Catalog.ForStore(StoreKind.Apple).Where(r => r.Category == "screenshots" && r.Id.Contains("iphone") && !r.Id.Contains("duo")).ToList();

        var slots = MediaSlotEvaluator.Evaluate(rules, new Dictionary<string, int>(), _today);

        slots.Where(s => s.Required).Should().OnlyContain(s => s.Fill == MediaSlotFill.None).And.NotBeEmpty();
    }

    private static string ManifestWith(string version)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(RulesFolder(), "catalog.json")));
        manifest["catalogVersion"] = version;

        return manifest.ToJsonString();
    }

    private static string ReadFromRepository(string path) => File.ReadAllText(Path.Combine(RulesFolder(), path));
}
