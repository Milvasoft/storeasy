using System.Globalization;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// The ASO scores, computed only from what the stores return (never from a model). All are estimates on a 0 to 100 scale.
/// <list type="bullet">
/// <item><b>Popularity</b> (<see cref="Popularity"/>): how early the store's own search autocomplete offers the keyword.
/// The keyword is typed letter by letter in steps (<see cref="PrefixLengths"/>, searched with a binary search); the
/// shorter the prefix at which the keyword appears among the suggestions, and the higher it is listed there, the more
/// people search it. <c>10 + 75 x ease + 15 x position</c>, where ease is 1 when one letter is enough and 0 when only the
/// whole keyword brings it, and position is 1 for the first suggestion. A keyword the autocomplete never offers scores 5
/// (other terms around it are searched) or 0 (nothing is).</item>
/// <item><b>Difficulty</b> (<see cref="Difficulty"/>): how strong the first ten search results are.
/// <c>100 x (0.55 x ratings + 0.10 x stars + 0.20 x title match + 0.15 x crowding)</c>: ratings is the mean of
/// log10(1 + rating count) / 5 capped at 1 (100,000 ratings count fully), stars the mean average rating / 5, title match
/// the share of the ten whose name contains every word of the keyword, crowding the result count / 200 capped at 1.</item>
/// <item><b>Opportunity</b> (<see cref="Opportunity"/>): the geometric mean of popularity and ease (100 - difficulty), so a
/// keyword needs both demand and room to score high.</item>
/// </list>
/// </summary>
public static class AsoScoring
{
    /// <summary>
    /// Popularity of a keyword the autocomplete never offers while it offers other terms for it.
    /// </summary>
    public const int NotSuggestedPopularity = 5;

    /// <summary>
    /// Results that count as a fully crowded keyword (the iTunes Search API's maximum).
    /// </summary>
    public const int CrowdedResultCount = 200;

    /// <summary>
    /// Rating count whose log10 is 5: an app with this many ratings counts as fully established.
    /// </summary>
    public const double EstablishedRatingCountLog = 5;

    /// <summary>
    /// How many results difficulty looks at.
    /// </summary>
    public const int TopCount = 10;

    private static readonly int[] _ladder = [1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50, 65, 80];

    /// <summary>
    /// Prefix lengths tried for a keyword of <paramref name="length"/> characters, shortest first, the keyword itself
    /// excluded.
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static List<int> PrefixLengths(int length) => [.. _ladder.Where(l => l < length)];

    /// <summary>
    /// Popularity from where the keyword first appeared: the prefix length that brought it, its index among the
    /// suggestions of that prefix and how many suggestions there were. <paramref name="foundPrefixLength"/> null means
    /// it never appeared; then <paramref name="anySuggestions"/> tells whether the keyword itself had suggestions.
    /// </summary>
    /// <param name="keywordLength"></param>
    /// <param name="foundPrefixLength"></param>
    /// <param name="position">0 based index among the suggestions.</param>
    /// <param name="suggestionCount"></param>
    /// <param name="anySuggestions"></param>
    /// <returns></returns>
    public static int Popularity(int keywordLength, int? foundPrefixLength, int position, int suggestionCount, bool anySuggestions)
    {
        if (foundPrefixLength == null)
            return anySuggestions ? NotSuggestedPopularity : 0;

        var prefix = Math.Clamp(foundPrefixLength.Value, 1, Math.Max(1, keywordLength));
        var ease = keywordLength <= 1 ? 1d : (keywordLength - prefix) / (double)(keywordLength - 1);
        var place = 1d - (Math.Max(0, position) / (double)Math.Max(1, suggestionCount));

        return Math.Clamp((int)Math.Round(10 + (75 * ease) + (15 * place), MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>
    /// Measures the popularity of <paramref name="keyword"/> (normalized) with an autocomplete: the keyword itself first
    /// (its suggestions are also the related terms), then a binary search over <see cref="PrefixLengths"/> for the
    /// shortest prefix that still offers it, at most <paramref name="maxQueries"/> calls in all. <paramref name="suggest"/>
    /// answers the normalized suggestions of a prefix, or null when the source failed; a failed first call gives a null
    /// popularity.
    /// </summary>
    /// <param name="keyword"></param>
    /// <param name="maxQueries"></param>
    /// <param name="suggest"></param>
    /// <returns></returns>
    public static async Task<PopularityMeasurement> MeasurePopularityAsync(string keyword, int maxQueries, Func<string, Task<IReadOnlyList<string>>> suggest)
    {
        var full = await suggest(keyword);

        if (full == null)
            return new PopularityMeasurement(null, [], 1);

        var related = full.Where(h => !string.Equals(h, keyword, StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
        var index = IndexOf(full, keyword);

        if (index < 0)
            return new PopularityMeasurement(Popularity(keyword.Length, null, 0, 0, full.Count > 0), related, 1);

        var best = (Length: keyword.Length, Index: index, Count: full.Count);
        var ladder = PrefixLengths(keyword.Length);
        var low = 0;
        var high = ladder.Count - 1;
        var calls = 1;

        while (low <= high && calls < Math.Max(1, maxQueries))
        {
            var middle = (low + high) / 2;
            var hints = await suggest(AsoText.Prefix(keyword, ladder[middle]));

            calls++;

            if (hints == null)
                break;

            var found = IndexOf(hints, keyword);

            if (found >= 0)
            {
                best = (ladder[middle], found, hints.Count);
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return new PopularityMeasurement(Popularity(keyword.Length, best.Length, best.Index, best.Count, true), related, calls);
    }

    /// <summary>
    /// Difficulty from the first <see cref="TopCount"/> results and the result count; null when the results carry no
    /// ratings at all (a source that lists ids only).
    /// </summary>
    /// <param name="results">Search results in order.</param>
    /// <param name="resultCount">How many results the search returned.</param>
    /// <param name="keyword">Normalized keyword.</param>
    /// <param name="culture">Culture of the keyword's language.</param>
    /// <returns></returns>
    public static int? Difficulty(IReadOnlyList<AsoCompetitor> results, int resultCount, string keyword, CultureInfo culture)
    {
        var top = (results ?? []).Take(TopCount).ToList();

        if (top.Count == 0)
            return resultCount <= 0 ? 0 : null;

        if (top.All(r => r.RatingCount == null && r.Rating == null))
            return null;

        var words = AsoText.Words(keyword, culture);
        var ratings = top.Average(r => Math.Min(1d, Math.Log10(1 + Math.Max(0, r.RatingCount ?? 0)) / EstablishedRatingCountLog));
        var stars = top.Average(r => Math.Clamp(r.Rating ?? 0, 0, 5) / 5d);
        var titleMatch = words.Count == 0 ? 0 : top.Average(r => ContainsAll(AsoText.Words(r.Name, culture), words) ? 1d : 0d);
        var crowding = Math.Min(1d, Math.Max(0, resultCount) / (double)CrowdedResultCount);
        var score = 100 * ((0.55 * ratings) + (0.10 * stars) + (0.20 * titleMatch) + (0.15 * crowding));

        return Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>
    /// Opportunity: the geometric mean of popularity and 100 - difficulty; null when either is unknown.
    /// </summary>
    /// <param name="popularity"></param>
    /// <param name="difficulty"></param>
    /// <returns></returns>
    public static int? Opportunity(int? popularity, int? difficulty)
    {
        if (popularity == null || difficulty == null)
            return null;

        var value = Math.Sqrt(Math.Clamp(popularity.Value, 0, 100) * (100d - Math.Clamp(difficulty.Value, 0, 100)));

        return Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);
    }

    /// <summary>
    /// Rank change between two checks, positive when the app moved up (rank 12 to 8 is +4); null when either day was not
    /// ranked or not checked.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="previous"></param>
    /// <returns></returns>
    public static int? RankChange(int? current, int? previous) => current == null || previous == null ? null : previous.Value - current.Value;

    private static int IndexOf(IReadOnlyList<string> hints, string keyword)
    {
        for (var index = 0; index < hints.Count; index++)
        {
            if (string.Equals(hints[index], keyword, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    private static bool ContainsAll(List<string> nameWords, List<string> words) => words.All(nameWords.Contains);
}

/// <summary>
/// Result of <see cref="AsoScoring.MeasurePopularityAsync"/>.
/// </summary>
/// <param name="Popularity">0 to 100, or null when the source failed.</param>
/// <param name="RelatedTerms">The other suggestions of the keyword itself.</param>
/// <param name="Calls">Autocomplete calls made.</param>
public sealed record PopularityMeasurement(int? Popularity, IReadOnlyList<string> RelatedTerms, int Calls);
