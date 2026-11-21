using System.Globalization;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// Keyword seeds taken from texts: the app's own listing (name, subtitle, keywords field, the description's most used words
/// and word pairs) and the names of competing apps (words two or more of them share). Seeds are only candidates; research
/// measures them.
/// </summary>
public static class AsoSeeds
{
    /// <summary>
    /// Seed source: the app name.
    /// </summary>
    public const string NameSource = "name";

    /// <summary>
    /// Seed source: the subtitle or short description.
    /// </summary>
    public const string SubtitleSource = "subtitle";

    /// <summary>
    /// Seed source: the App Store keywords field.
    /// </summary>
    public const string KeywordsSource = "keywords";

    /// <summary>
    /// Seed source: the description.
    /// </summary>
    public const string DescriptionSource = "description";

    /// <summary>
    /// Seed source: competitors' names.
    /// </summary>
    public const string CompetitorSource = "competitors";

    /// <summary>
    /// Seed source: the store's search suggestions.
    /// </summary>
    public const string SuggestionSource = "suggestions";

    /// <summary>
    /// Seed source: the organization's AI.
    /// </summary>
    public const string AiSource = "ai";

    /// <summary>
    /// Most description seeds.
    /// </summary>
    public const int MaxDescriptionSeeds = 12;

    private const int _minWordLength = 3;

    // Dashes are given by code point (the en and em dash).
    private static readonly char[] _titleSeparators = [':', '-', '|', (char)0x2013, (char)0x2014, '(', ')', ',', '&', '+', '/'];

    // Words that are never a keyword on their own (English and Turkish function words, store boilerplate), written without
    // diacritics: a word is compared after Fold, so a Turkish word matches its plain form.
    private static readonly HashSet<string> _stopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "with", "your", "you", "are", "this", "that", "from", "app", "apps", "all", "can", "more", "our",
        "use", "new", "get", "has", "have", "will", "any", "not", "its", "into", "out", "now", "one", "just", "also", "free",
        "best", "who", "how", "what", "when", "where", "which", "while", "than", "then", "them", "they", "their", "there",
        "here", "been", "was", "were", "about", "each", "every", "other", "some", "such", "only", "very", "most", "make",
        "ile", "bir", "bu", "icin", "gibi", "her", "veya", "cok", "olarak", "olan", "sen", "siz", "kadar", "ama", "daha",
        "sadece", "tum", "uygulama", "uygulamasi", "sizin", "senin", "ise", "ancak", "hem", "yeni", "ayrica",
    };

    /// <summary>
    /// Seeds of the app's own listing, each with its source, in this order: name parts, subtitle parts, keywords field
    /// terms, description words and pairs (most used first). A seed found in several places keeps the first source.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="subtitle"></param>
    /// <param name="keywords"></param>
    /// <param name="description"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static List<(string Term, string Source)> FromListing(string name, string subtitle, string keywords, string description, CultureInfo culture)
    {
        var seeds = new List<(string Term, string Source)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string term, string source)
        {
            var normalized = AsoText.Normalize(term, culture);

            if (normalized != null && IsUseful(normalized, culture) && seen.Add(normalized))
                seeds.Add((normalized, source));
        }

        foreach (var part in Parts(name))
            Add(part, NameSource);

        foreach (var part in Parts(subtitle))
            Add(part, SubtitleSource);

        foreach (var word in AsoText.Words($"{name} {subtitle}", culture))
            Add(word, NameSource);

        foreach (var term in (keywords ?? string.Empty).Split(','))
            Add(term, KeywordsSource);

        foreach (var term in DescriptionTerms(description, culture))
            Add(term, DescriptionSource);

        return seeds;
    }

    /// <summary>
    /// Words and word pairs that at least two of the competing apps' names share, most shared first.
    /// </summary>
    /// <param name="names"></param>
    /// <param name="culture"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static List<string> FromCompetitorNames(IEnumerable<string> names, CultureInfo culture, int max = 15)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var name in names ?? [])
        {
            var terms = new HashSet<string>(StringComparer.Ordinal);

            foreach (var part in Parts(name))
            {
                var words = AsoText.Words(part, culture).Where(w => IsUseful(w, culture)).ToList();

                terms.UnionWith(words);

                for (var index = 0; index + 1 < words.Count; index++)
                    terms.Add($"{words[index]} {words[index + 1]}");
            }

            foreach (var term in terms)
                counts[term] = counts.GetValueOrDefault(term) + 1;
        }

        return [.. counts.Where(c => c.Value >= 2).OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).Take(max).Select(c => c.Key)];
    }

    /// <summary>
    /// The description's most used words and word pairs (function words left out), most used first.
    /// </summary>
    /// <param name="description"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public static List<string> DescriptionTerms(string description, CultureInfo culture)
    {
        var words = AsoText.Words(description, culture);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var index = 0; index < words.Count; index++)
        {
            if (!IsUseful(words[index], culture))
                continue;

            counts[words[index]] = counts.GetValueOrDefault(words[index]) + 1;

            if (index + 1 < words.Count && IsUseful(words[index + 1], culture))
            {
                var pair = $"{words[index]} {words[index + 1]}";

                // A pair counts double: it says more about the app than either word.
                counts[pair] = counts.GetValueOrDefault(pair) + 2;
            }
        }

        return [.. counts.Where(c => c.Value >= 2).OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).Take(MaxDescriptionSeeds).Select(c => c.Key)];
    }

    /// <summary>
    /// <paramref name="word"/> without diacritics, the dotless i read as i.
    /// </summary>
    /// <param name="word"></param>
    /// <returns></returns>
    public static string Fold(string word)
    {
        var decomposed = word.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(character == (char)0x131 ? 'i' : character);
        }

        return builder.ToString();
    }

    private static IEnumerable<string> Parts(string text)
        => string.IsNullOrWhiteSpace(text) ? [] : text.Split(_titleSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsUseful(string term, CultureInfo culture)
    {
        var words = AsoText.Words(term, culture);

        if (words.Count == 0 || words.All(w => _stopWords.Contains(Fold(w))))
            return false;

        return words.Count > 1 || (words[0].Length >= _minWordLength && !words[0].All(char.IsDigit));
    }
}
