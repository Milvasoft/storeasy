using FluentAssertions;
using Storeasy.Domain.Studio;

namespace Storeasy.UnitTests.StudioTests;

[Trait("Studio Unit Tests", "Caption line breaking.")]
public class CaptionLineBreakerTests
{
    // One unit per text element: easy to reason about widths.
    private static float Measure(string text) => new System.Globalization.StringInfo(text).LengthInTextElements;

    [Fact]
    public void Break_ShouldWrapAtSpacesGreedily()
    {
        CaptionLineBreaker.Break("Plan your day at a glance", 12, Measure).Should().Equal("Plan your", "day at a", "glance");
    }

    [Fact]
    public void Break_Turkish_ShouldKeepWordsWhole()
    {
        CaptionLineBreaker.Break("Gününü tek bakışta planla", 14, Measure).Should().Equal("Gününü tek", "bakışta planla");
    }

    [Fact]
    public void Break_ManualNewLine_ShouldStartANewLine()
    {
        CaptionLineBreaker.Break("Check off\nevery step", 40, Measure).Should().Equal("Check off", "every step");
    }

    [Fact]
    public void Break_LongWord_ShouldSplitBetweenCharacters()
    {
        CaptionLineBreaker.Break("Donaudampfschifffahrt", 8, Measure).Should().Equal("Donaudam", "pfschiff", "fahrt");
    }

    [Fact]
    public void Break_Hyphen_ShouldBeABreakOpportunity()
    {
        CaptionLineBreaker.Break("state-of-the-art", 9, Measure).Should().Equal("state-of-", "the-art");
    }

    [Fact]
    public void Break_Japanese_ShouldBreakBetweenCharactersButNotBeforeClosingPunctuation()
    {
        // "毎日の目標。" = six characters ending with an ideographic full stop.
        var lines = CaptionLineBreaker.Break("毎日の目標。前進", 5, Measure);

        lines.Should().HaveCount(2);
        lines[1].Should().NotStartWith("。", "a full stop never starts a line");
        string.Concat(lines).Should().Be("毎日の目標。前進");
    }

    [Fact]
    public void Break_SmallKana_ShouldNotStartALine()
    {
        var lines = CaptionLineBreaker.Break("チェックしよう", 3, Measure);

        lines.Should().NotContain(line => line.StartsWith('ェ') || line.StartsWith('ッ'));
    }

    [Fact]
    public void Break_Korean_ShouldBreakAtSpaces()
    {
        CaptionLineBreaker.Break("하루를 한눈에 계획하세요", 7, Measure)
                          .Should().Equal("하루를 한눈에", "계획하세요");
    }

    [Fact]
    public void Break_CombiningMarks_ShouldStayWithTheirLetter()
    {
        // Thai without spaces falls back to grapheme breaking; the vowel mark stays on its consonant.
        var lines = CaptionLineBreaker.Break("กินข้าว", 2, Measure);

        lines.Should().AllSatisfy(line => char.GetUnicodeCategory(line[0]).Should().NotBe(System.Globalization.UnicodeCategory.NonSpacingMark));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Break_Empty_ShouldGiveNoLines(string text)
    {
        CaptionLineBreaker.Break(text, 10, Measure).Should().BeEmpty();
    }
}
