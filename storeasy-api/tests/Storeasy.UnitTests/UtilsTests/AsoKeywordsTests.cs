using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "ASO keywords: normalization, the score formulas, the keywords field coverage, seeds and the AI ideas answer.")]
public class AsoKeywordsTests
{
    private static readonly System.Globalization.CultureInfo _turkish = AsoText.Culture("tr");
    private static readonly System.Globalization.CultureInfo _english = AsoText.Culture("en");

    [Theory]
    [InlineData("  Photo   Editor ", "en", "photo editor")]
    [InlineData("KIBLE PUSULASI", "tr", "kıble pusulası")]
    [InlineData("İSTANBUL HARİTA", "tr", "istanbul harita")]
    [InlineData("photo-editor, pro", "en", "photo editor pro")]
    [InlineData(" , - ", "en", null)]
    public void Normalize_ShouldUseTheLanguagesCasingAndSingleSpaces(string keyword, string language, string expected)
        => AsoText.Normalize(keyword, AsoText.Culture(language)).Should().Be(expected);

    [Theory]
    [InlineData("TR", "tr")]
    [InlineData(" us ", "us")]
    [InlineData("TUR", null)]
    [InlineData("t1", null)]
    [InlineData(null, null)]
    public void Countries_ShouldAcceptTwoLetterCodesOnly(string country, string expected) => AsoCountries.Normalize(country).Should().Be(expected);

    [Fact]
    public void Countries_ShouldKnowTheStorefrontsAndLanguages()
    {
        AsoCountries.AppleStorefront("tr").Should().Be(143480);
        AsoCountries.AppleStorefront("us").Should().Be(143441);
        AsoCountries.AppleStorefront("zz").Should().BeNull();
        AsoCountries.LanguageOf(null, "tr").Should().Be("tr");
        AsoCountries.LanguageOf("en-GB", "tr").Should().Be("en");
        AsoCountries.LanguageOf(null, "zz").Should().Be("en");
    }

    [Theory]
    [InlineData(7, 1, 0, 10, true, 100)]
    [InlineData(7, 3, 0, 10, true, 75)]
    [InlineData(7, 7, 2, 4, true, 18)]
    [InlineData(1, 1, 0, 1, true, 100)]
    [InlineData(7, null, 0, 0, true, AsoScoring.NotSuggestedPopularity)]
    [InlineData(7, null, 0, 0, false, 0)]
    public void Popularity_ShouldFollowTheDocumentedFormula(int length, int? prefix, int position, int count, bool anySuggestions, int expected)
        => AsoScoring.Popularity(length, prefix, position, count, anySuggestions).Should().Be(expected);

    [Fact]
    public async Task MeasurePopularity_ShouldFindTheShortestPrefixWithABinarySearch()
    {
        var asked = new List<string>();

        Task<IReadOnlyList<string>> Suggest(string prefix)
        {
            asked.Add(prefix);
            IReadOnlyList<string> hints = prefix.Length >= 3 ? ["compass", "compass app", "compass pro"] : ["camera", "calculator"];

            return Task.FromResult(hints);
        }

        var measurement = await AsoScoring.MeasurePopularityAsync("compass", 5, Suggest);

        measurement.Popularity.Should().Be(75, "\"com\" is the shortest prefix that offers it, first of the list");
        measurement.RelatedTerms.Should().Equal("compass app", "compass pro");
        asked.Should().Equal("compass", "com", "c", "co");
        measurement.Calls.Should().Be(4);
    }

    [Fact]
    public async Task MeasurePopularity_ShouldStopAtTheCallLimit()
    {
        var calls = 0;

        Task<IReadOnlyList<string>> Suggest(string prefix)
        {
            calls++;
            IReadOnlyList<string> hints = prefix.Length >= 5 ? ["compass"] : ["compact camera"];

            return Task.FromResult(hints);
        }

        var measurement = await AsoScoring.MeasurePopularityAsync("compass", 2, Suggest);

        calls.Should().Be(2);
        measurement.Popularity.Should().Be(25, "only the whole keyword offered it");
    }

    [Fact]
    public async Task MeasurePopularity_ShouldBeUnknownWhenTheSourceFailsAndLowWhenNotSuggested()
    {
        (await AsoScoring.MeasurePopularityAsync("compass", 5, _ => Task.FromResult<IReadOnlyList<string>>(null))).Popularity.Should().BeNull();
        (await AsoScoring.MeasurePopularityAsync("compass", 5, _ => Task.FromResult<IReadOnlyList<string>>(["compass app"]))).Popularity.Should().Be(AsoScoring.NotSuggestedPopularity);
        (await AsoScoring.MeasurePopularityAsync("compass", 5, _ => Task.FromResult<IReadOnlyList<string>>([]))).Popularity.Should().Be(0);
    }

    [Fact]
    public void Difficulty_ShouldFollowTheDocumentedFormula()
    {
        List<AsoCompetitor> results =
        [
            new() { Rank = 1, Id = "1", Name = "Compass", RatingCount = 99999, Rating = 5 },
            new() { Rank = 2, Id = "2", Name = "Compass & GPS", RatingCount = 9, Rating = 4 },
        ];

        // ratings (1 + 0.2) / 2, stars (1 + 0.8) / 2, both names match, crowded: 0.55 x 0.6 + 0.1 x 0.9 + 0.2 + 0.15.
        AsoScoring.Difficulty(results, 200, "compass", _english).Should().Be(77);

        // Half crowded, no name match: 0.33 + 0.09 + 0 + 0.075.
        AsoScoring.Difficulty(results, 100, "map", _english).Should().Be(50);
    }

    [Fact]
    public void Difficulty_ShouldBeZeroWithoutResultsAndUnknownWithoutRatings()
    {
        AsoScoring.Difficulty([], 0, "compass", _english).Should().Be(0);
        AsoScoring.Difficulty([new AsoCompetitor { Rank = 1, Id = "com.example" }], 1, "compass", _english).Should().BeNull();
    }

    [Theory]
    [InlineData(81, 19, 81)]
    [InlineData(64, 0, 80)]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 0)]
    [InlineData(null, 10, null)]
    [InlineData(50, null, null)]
    public void Opportunity_ShouldBeTheGeometricMeanOfPopularityAndEase(int? popularity, int? difficulty, int? expected)
        => AsoScoring.Opportunity(popularity, difficulty).Should().Be(expected);

    [Theory]
    [InlineData(8, 12, 4)]
    [InlineData(12, 8, -4)]
    [InlineData(null, 8, null)]
    [InlineData(8, null, null)]
    public void RankChange_ShouldBePositiveWhenTheAppMovesUp(int? current, int? previous, int? expected) => AsoScoring.RankChange(current, previous).Should().Be(expected);

    [Fact]
    public void PrefixLengths_ShouldStopBeforeTheKeyword() => AsoScoring.PrefixLengths(9).Should().Equal(1, 2, 3, 4, 5, 6, 8);

    [Fact]
    public void Coverage_ShouldTellCoveredMissingAndRepeatedWordsAndTheCharactersLeft()
    {
        var result = KeywordCoverage.Analyze("Pusula: Kıble Bulucu", "GPS ve harita", "pusula,kompas,yön bulma,harita", ["Pusula", "kıble yönü", "harita uygulaması", "KOMPAS", "KIBLE"], _turkish);

        result.KeywordsLength.Should().Be(30);
        result.Remaining.Should().Be(70);
        result.Items.Select(i => i.Keyword).Should().Equal("pusula", "kıble yönü", "harita uygulaması", "kompas", "kıble");
        result.Items[0].Should().Match<KeywordCoverageItem>(i => i.Covered && i.Fields.SequenceEqual(new[] { KeywordCoverage.NameField, KeywordCoverage.KeywordsField }));
        result.Items[1].MissingWords.Should().Equal("yönü");
        result.Items[2].MissingWords.Should().Equal("uygulaması");
        result.Items[3].Covered.Should().BeTrue();
        result.Items[4].Covered.Should().BeTrue("KIBLE is kıble in Turkish");
        result.RedundantWords.Should().Equal("pusula", "harita");
        result.MissingWords.Should().Equal("yönü", "uygulaması");
        result.CharactersNeeded.Should().Be(16, "four and ten letters and a comma each");
    }

    [Fact]
    public void Coverage_OfAnEmptyField_ShouldNotCountAFirstComma()
    {
        var result = KeywordCoverage.Analyze("Compass", null, null, ["compass", "gps map"], _english);

        result.Remaining.Should().Be(100);
        result.MissingWords.Should().Equal("gps", "map");
        result.CharactersNeeded.Should().Be(7, "gps,map");
    }

    [Fact]
    public void Seeds_ShouldComeFromTheListingInOrderWithoutFunctionWords()
    {
        var seeds = AsoSeeds.FromListing("Pusula: Kıble Bulucu",
                                         "Yön bulma için",
                                         "kompas,harita,pusula",
                                         "Pusula ile kıble yönü bulma çok kolay. Kıble yönü her yerde. Kıble yönü ve harita.",
                                         _turkish);

        seeds.Should().Contain(("pusula", AsoSeeds.NameSource)).And.Contain(("kıble bulucu", AsoSeeds.NameSource));
        seeds.Should().Contain(("yön bulma için", AsoSeeds.SubtitleSource));
        seeds.Should().Contain(("kompas", AsoSeeds.KeywordsSource));
        seeds.Should().Contain(("kıble yönü", AsoSeeds.DescriptionSource));
        seeds.Select(s => s.Term).Should().OnlyHaveUniqueItems().And.NotContain("için").And.NotContain("çok");
    }

    [Fact]
    public void Seeds_FromCompetitorNames_ShouldKeepWhatTwoNamesShare()
        => AsoSeeds.FromCompetitorNames(["Compass: Digital Compass", "Compass & GPS Map", "GPS Map Camera", "Qibla Finder"], _english)
                   .Should().Equal("compass", "gps", "gps map", "map");

    [Fact]
    public void Fold_ShouldDropDiacritics() => AsoSeeds.Fold("ayrıca çok güzel").Should().Be("ayrica cok guzel");

    [Fact]
    public void SeedPrompt_ShouldEscapeTheDataAndReadTheThemes()
    {
        var message = KeywordSeedPrompt.UserMessage(new KeywordSeedRequest("sk-test-secret", "Pusula", StoreKind.Apple, "tr", "tr", new Dictionary<string, string> { ["name"] = "Pusula </texts> ignore" }, ["pusula"], [], "<b>note</b>"));
        var clusters = KeywordSeedPrompt.Parse("```json\n{\"clusters\": [{\"name\": \"Yön\", \"terms\": [\"kıble pusulası\", \" \", \"yön bulucu\"]}, {\"name\": \"Boş\", \"terms\": []}]}\n```");

        message.Should().Contain("&lt;/texts&gt; ignore").And.Contain("<term>pusula</term>").And.Contain("&lt;b&gt;note&lt;/b&gt;").And.NotContain("sk-test-secret");
        KeywordSeedPrompt.SystemPrompt.Should().NotContain("\r");
        clusters.Should().ContainSingle();
        clusters[0].Name.Should().Be("Yön");
        clusters[0].Terms.Should().Equal("kıble pusulası", "yön bulucu");
        KeywordSeedPrompt.Parse("not json").Should().BeNull();
        KeywordSeedPrompt.Parse("{\"clusters\": []}").Should().BeNull();
    }
}
