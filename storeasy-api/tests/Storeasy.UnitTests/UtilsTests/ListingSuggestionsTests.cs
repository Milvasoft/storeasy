using FluentAssertions;
using Storeasy.Application.Utils.Ai;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Enums;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "AI listing suggestions: supported fields, fitting into the limits, reading the answer.")]
public class ListingSuggestionsTests
{
    [Theory]
    [InlineData(StoreKind.Apple, "keywords", true)]
    [InlineData(StoreKind.Apple, "whatsNew", true)]
    [InlineData(StoreKind.Apple, "supportUrl", false)]
    [InlineData(StoreKind.Apple, "eula", false)]
    [InlineData(StoreKind.Google, "shortDescription", true)]
    [InlineData(StoreKind.Google, "contactEmail", false)]
    [InlineData(StoreKind.Microsoft, "description", false)]
    public void Find_ShouldOfferOnlyTheListingTexts(StoreKind store, string field, bool expected)
        => (ListingSuggestions.Find(store, field) != null).Should().Be(expected);

    [Fact]
    public void Fit_ShouldKeepAFittingTextAsItIs()
        => ListingSuggestions.Fit("subtitle", "  Plan your day  ", 30, null).Should().Be(("Plan your day", false));

    [Fact]
    public void Fit_ShouldCutALongTextAtASentence()
    {
        var text = "First sentence is here. Second sentence is a little longer than the first one.";

        var fitted = ListingSuggestions.Fit("description", text, 40, null);

        fitted.Should().Be(("First sentence is here.", true));
    }

    [Fact]
    public void Fit_ShouldCutAtAWordWhenNoSentenceFits()
    {
        var fitted = ListingSuggestions.Fit("subtitle", "Calm focus timer for deep work sessions", 20, null);

        fitted.Value.Value.Should().Be("Calm focus timer for");
        fitted.Value.Shortened.Should().BeTrue();
    }

    [Fact]
    public void Fit_ShouldDedupeKeywordsAndDropTheLastOnesOverTheBytes()
    {
        var fitted = ListingSuggestions.Fit("keywords", "map, compass,Map,şehir,yürüyüş", null, 20);

        fitted.Should().Be(("map,compass,şehir", true));
    }

    [Theory]
    [InlineData("{\"value\": \"Hello\", \"reason\": \"Short.\"}", "Hello", "Short.")]
    [InlineData("```json\n{\"value\": \"Hello\"}\n```", "Hello", null)]
    [InlineData("Here it is: {\"value\": \" Hi \", \"reason\": \"\"} done", "Hi", null)]
    public void Parse_ShouldReadTheValueAndTheReason(string text, string value, string reason)
        => ListingSuggestionPrompt.Parse(text).Should().Be((value, reason));

    [Theory]
    [InlineData("no json")]
    [InlineData("{\"reason\": \"no value\"}")]
    [InlineData("{\"value\": \"\"}")]
    [InlineData("{\"value\": 12}")]
    [InlineData("{broken")]
    public void Parse_ShouldRefuseAnAnswerWithoutAValue(string text)
        => ListingSuggestionPrompt.Parse(text).Should().BeNull();

    [Theory]
    [InlineData("{\"primary\": \"TRAVEL\", \"secondary\": \"NAVIGATION\", \"reason\": \"Why.\"}", "TRAVEL", "NAVIGATION", "Why.")]
    [InlineData("{\"primary\": \"TRAVEL\", \"secondary\": null}", "TRAVEL", null, null)]
    [InlineData("```json {\"primary\": \" TRAVEL \", \"secondary\": \"none\"} ```", "TRAVEL", null, null)]
    public void ParseCategories_ShouldReadThePrimaryTheSecondaryAndTheReason(string text, string primary, string secondary, string reason)
        => CategorySuggestionPrompt.Parse(text).Should().Be((primary, secondary, reason));

    [Theory]
    [InlineData("{\"secondary\": \"TRAVEL\"}")]
    [InlineData("nothing")]
    public void ParseCategories_ShouldRefuseAnAnswerWithoutAPrimary(string text)
        => CategorySuggestionPrompt.Parse(text).Should().BeNull();
}
