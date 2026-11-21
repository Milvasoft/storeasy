using Storeasy.Domain.Content;
using System.Globalization;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// Apple's keywords field helper. The App Store matches searches against the words of the app name, the subtitle and the
/// 100 character keywords field together, so a tracked keyword is covered when every one of its words appears in one of
/// them. The helper tells which tracked keywords are covered, which words are missing, which words of the keywords field
/// only repeat the name or the subtitle (characters spent for nothing) and how many characters are left.
/// </summary>
public static class KeywordCoverage
{
    /// <summary>
    /// Apple's limit of the keywords field.
    /// </summary>
    public const int KeywordsFieldLimit = 100;

    /// <summary>
    /// Field name of the app name in the result.
    /// </summary>
    public const string NameField = "name";

    /// <summary>
    /// Field name of the subtitle in the result.
    /// </summary>
    public const string SubtitleField = "subtitle";

    /// <summary>
    /// Field name of the keywords field in the result.
    /// </summary>
    public const string KeywordsField = "keywords";

    /// <summary>
    /// Analyzes one locale.
    /// </summary>
    /// <param name="name">App name of the locale.</param>
    /// <param name="subtitle">Subtitle of the locale.</param>
    /// <param name="keywords">Keywords field of the locale (comma separated).</param>
    /// <param name="trackedKeywords">Keywords to check (any case; they are normalized with <paramref name="culture"/>).</param>
    /// <param name="culture">Culture of the locale's language.</param>
    /// <returns></returns>
    public static KeywordCoverageResult Analyze(string name, string subtitle, string keywords, IEnumerable<string> trackedKeywords, CultureInfo culture)
    {
        var nameWords = AsoText.Words(name, culture).ToHashSet(StringComparer.Ordinal);
        var subtitleWords = AsoText.Words(subtitle, culture).ToHashSet(StringComparer.Ordinal);
        var terms = (keywords ?? string.Empty).Split(',').Select(t => AsoText.Normalize(t, culture)).Where(t => t != null).ToList();
        var keywordWords = terms.SelectMany(t => AsoText.Words(t, culture)).ToHashSet(StringComparer.Ordinal);

        var items = new List<KeywordCoverageItem>();

        foreach (var keyword in (trackedKeywords ?? []).Select(k => AsoText.Normalize(k, culture)).Where(k => k != null).Distinct(StringComparer.Ordinal))
        {
            var words = AsoText.Words(keyword, culture).Distinct(StringComparer.Ordinal).ToList();
            var missing = words.Where(w => !nameWords.Contains(w) && !subtitleWords.Contains(w) && !keywordWords.Contains(w)).ToList();
            var fields = new List<string>();

            if (words.Any(nameWords.Contains))
                fields.Add(NameField);

            if (words.Any(subtitleWords.Contains))
                fields.Add(SubtitleField);

            if (words.Any(keywordWords.Contains))
                fields.Add(KeywordsField);

            items.Add(new KeywordCoverageItem(keyword, missing.Count == 0, missing, fields));
        }

        // A keywords field word that the name or the subtitle already has, or that the field repeats, spends characters for
        // nothing (Apple combines the fields).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var redundant = new List<string>();

        foreach (var word in terms.SelectMany(t => AsoText.Words(t, culture)))
        {
            if ((nameWords.Contains(word) || subtitleWords.Contains(word) || !seen.Add(word)) && !redundant.Contains(word))
                redundant.Add(word);
        }

        var length = ContentDiff.CharCount(keywords ?? string.Empty);
        var missingWords = items.SelectMany(i => i.MissingWords).Distinct(StringComparer.Ordinal).ToList();

        // Adding a word costs its characters and a comma (none before the first one).
        var needed = missingWords.Sum(w => ContentDiff.CharCount(w)) + missingWords.Count - (length == 0 && missingWords.Count > 0 ? 1 : 0);

        return new KeywordCoverageResult(length, KeywordsFieldLimit - length, items, redundant, missingWords, needed);
    }
}

/// <summary>
/// One tracked keyword's coverage.
/// </summary>
/// <param name="Keyword">Normalized keyword.</param>
/// <param name="Covered">Whether every word is in the name, the subtitle or the keywords field.</param>
/// <param name="MissingWords">Words found in none of them.</param>
/// <param name="Fields">Fields that hold at least one of its words (<c>name</c>, <c>subtitle</c>, <c>keywords</c>).</param>
public sealed record KeywordCoverageItem(string Keyword, bool Covered, IReadOnlyList<string> MissingWords, IReadOnlyList<string> Fields);

/// <summary>
/// Result of <see cref="KeywordCoverage.Analyze"/>.
/// </summary>
/// <param name="KeywordsLength">Characters used in the keywords field.</param>
/// <param name="Remaining">Characters left of the 100 (negative when over).</param>
/// <param name="Items">Coverage of each keyword.</param>
/// <param name="RedundantWords">Keywords field words already in the name or subtitle, or repeated in the field.</param>
/// <param name="MissingWords">Every missing word, once.</param>
/// <param name="CharactersNeeded">Characters the missing words need in the keywords field, commas included.</param>
public sealed record KeywordCoverageResult(int KeywordsLength,
                                           int Remaining,
                                           IReadOnlyList<KeywordCoverageItem> Items,
                                           IReadOnlyList<string> RedundantWords,
                                           IReadOnlyList<string> MissingWords,
                                           int CharactersNeeded);
