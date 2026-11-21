using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Aso;
using Storeasy.Domain.Content;
using System.Globalization;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Shared steps of the ASO keyword handlers: keyword normalization per country, the listing texts a seed or coverage reads,
/// and the tracked keyword rows with their recent ranks and latest measurements.
/// </summary>
public static class AsoExtensions
{
    /// <summary>
    /// Days of the sparkline and of the longest change.
    /// </summary>
    public const int RecentDays = 30;

    /// <summary>
    /// The culture keywords of <paramref name="country"/> are normalized with (the country's usual language), so a keyword
    /// is stored the same way whoever types it.
    /// </summary>
    /// <param name="country">Lower case country code.</param>
    /// <returns></returns>
    public static CultureInfo KeywordCulture(string country) => AsoText.Culture(AsoCountries.Language(country));

    /// <summary>
    /// The keywords normalized for <paramref name="country"/>, without empty ones and duplicates, in the given order.
    /// </summary>
    /// <param name="keywords"></param>
    /// <param name="country"></param>
    /// <returns></returns>
    public static List<string> NormalizeKeywords(IEnumerable<string> keywords, string country)
    {
        var culture = KeywordCulture(country);

        return [.. (keywords ?? []).Select(k => AsoText.Normalize(k, culture)).Where(k => k != null && k.Length <= AsoTrackedKeyword.KeywordMaxLength).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The listing texts of one locale that keyword work reads: the name (title on Play), subtitle (short description), the
    /// App Store keywords field and the description.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="locale"></param>
    /// <returns></returns>
    public static (string Name, string Subtitle, string Keywords, string Description) ListingTexts(this StoreContentSnapshot snapshot, string locale)
    {
        if (snapshot == null || locale == null)
            return default;

        string Value(string field)
        {
            var definition = ListingFields.Find(snapshot.Store, field);

            return definition == null ? null : snapshot.Get(ContentKey.Of(definition.Group, locale, definition.Field));
        }

        return snapshot.Store == StoreKind.Google
            ? (Value("title"), Value("shortDescription"), null, Value("fullDescription"))
            : (Value("name"), Value("subtitle"), Value("keywords"), Value("description"));
    }

    /// <summary>
    /// The listing locale that matches <paramref name="requested"/>, else one in the country's language, else the primary.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <param name="requested"></param>
    /// <param name="country"></param>
    /// <returns></returns>
    public static string ListingLocaleFor(this StoreContentSnapshot snapshot, string requested, string country)
    {
        if (snapshot == null)
            return null;

        var locales = snapshot.Locales ?? [];
        var exact = locales.FirstOrDefault(l => string.Equals(l, requested, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
            return exact;

        var language = AsoCountries.Language(country);
        var byLanguage = locales.FirstOrDefault(l => string.Equals(l.Split('-', '_')[0], language, StringComparison.OrdinalIgnoreCase));

        return byLanguage ?? snapshot.PrimaryLocale ?? locales.FirstOrDefault();
    }

    /// <summary>
    /// Reads the listing texts live and picks the locale for <paramref name="country"/>. On a store failure the texts are
    /// empty and the message key of the failure is returned, so the caller can go on without them.
    /// </summary>
    /// <param name="contentService"></param>
    /// <param name="app"></param>
    /// <param name="requestedLocale"></param>
    /// <param name="country"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<(string Locale, (string Name, string Subtitle, string Keywords, string Description) Texts, string ErrorKey)> ReadAsoListingAsync(this IStoreContentService contentService,
                                                                                                                                                             StoreAppContext app,
                                                                                                                                                             string requestedLocale,
                                                                                                                                                             string country,
                                                                                                                                                             CancellationToken cancellationToken)
    {
        var read = await contentService.ReadContentAsync(app, ContentParts.Texts, null, null, cancellationToken);

        if (!read.IsSuccess)
            return (null, default, read.ErrorKey);

        var locale = read.Snapshot.ListingLocaleFor(requestedLocale, country);

        return (locale, read.Snapshot.ListingTexts(locale), null);
    }

    /// <summary>
    /// The active tracked keywords of a store record in a country.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="storeAppId"></param>
    /// <param name="country"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<HashSet<string>> TrackedKeywordSetAsync(this IStoreasyRepositoryBase<AsoTrackedKeyword> repository, int storeAppId, string country, CancellationToken cancellationToken)
    {
        var rows = await repository.GetAllAsync(t => t.StoreAppId == storeAppId && t.Country == country && t.IsActive,
                                                projection: t => new AsoTrackedKeyword { Id = t.Id, Keyword = t.Keyword },
                                                cancellationToken: cancellationToken);

        return rows.Select(t => t.Keyword).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// A search result as a DTO.
    /// </summary>
    /// <param name="competitor"></param>
    /// <param name="ownAppId">Store id of the app itself.</param>
    /// <returns></returns>
    public static AsoCompetitorDto ToDto(this AsoCompetitor competitor, string ownAppId) => new()
    {
        Rank = competitor.Rank,
        Id = competitor.Id,
        Name = competitor.Name,
        Developer = competitor.Developer,
        Rating = competitor.Rating,
        RatingCount = competitor.RatingCount,
        IconUrl = competitor.IconUrl,
        IsOwnApp = ownAppId != null && string.Equals(competitor.Id, ownAppId, StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>
    /// A researched keyword as a DTO; <see cref="AsoKeywordResearchDto.Tracked"/> is left for the reader to set.
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store"></param>
    /// <param name="country">Lower case country code.</param>
    /// <param name="ownAppId">Store id of the app itself.</param>
    /// <returns></returns>
    public static AsoKeywordResearchDto ToResearchDto(this AsoResearchItem item, StoreKind store, string country, string ownAppId) => new()
    {
        Keyword = item.Metric.Keyword,
        Store = store,
        Country = country,
        Popularity = item.Metric.Popularity,
        PopularitySource = item.Metric.PopularitySource,
        Difficulty = item.Metric.Difficulty,
        Opportunity = AsoScoring.Opportunity(item.Metric.Popularity, item.Metric.Difficulty),
        CompetingApps = item.Metric.CompetingApps,
        CurrentRank = item.OwnRank,
        RankChecked = item.ResultsRead,
        Depth = item.Depth,
        TopCompetitors = [.. (item.Metric.TopCompetitors ?? []).Select(c => c.ToDto(ownAppId))],
        RelatedTerms = item.Metric.RelatedTerms ?? [],
        PopularityDate = item.Metric.PopularityDate,
        ResultsDate = item.Metric.ResultsDate,
    };

    /// <summary>
    /// The DTOs of tracked keyword rows with the last <see cref="RecentDays"/> days of ranks and the latest measurements,
    /// read in two queries.
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="rankRepository"></param>
    /// <param name="metricRepository"></param>
    /// <param name="today">Current UTC day.</param>
    /// <param name="depth">Results read per check.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<List<AsoTrackedKeywordDto>> ToDtosAsync(this IReadOnlyList<AsoTrackedKeyword> rows,
                                                                     IStoreasyRepositoryBase<AsoKeywordRank> rankRepository,
                                                                     IStoreasyRepositoryBase<AsoKeywordMetric> metricRepository,
                                                                     DateOnly today,
                                                                     int depth,
                                                                     CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(r => r.Id).ToList();
        var from = today.AddDays(-RecentDays);

        var ranks = await rankRepository.GetAllAsync(r => ids.Contains(r.TrackedKeywordId) && r.Date >= from,
                                                     projection: r => new AsoKeywordRank { TrackedKeywordId = r.TrackedKeywordId, Date = r.Date, Rank = r.Rank, Depth = r.Depth },
                                                     cancellationToken: cancellationToken);

        var keywords = rows.Select(r => r.Keyword).Distinct().ToList();
        var metrics = await metricRepository.GetAllAsync(m => keywords.Contains(m.Keyword),
                                                         projection: m => new AsoKeywordMetric { Store = m.Store, Country = m.Country, Keyword = m.Keyword, Popularity = m.Popularity, Difficulty = m.Difficulty },
                                                         cancellationToken: cancellationToken);

        var ranksById = ranks.GroupBy(r => r.TrackedKeywordId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date));

        return [.. rows.Select(row =>
        {
            var days = ranksById.GetValueOrDefault(row.Id) ?? [];
            var metric = metrics.FirstOrDefault(m => m.Store == row.Store && m.Country == row.Country && m.Keyword == row.Keyword);
            var latest = days.Count == 0 ? null : days[days.Keys.Max()];
            int? RankOn(DateOnly day) => days.TryGetValue(day, out var rank) ? rank.Rank : null;

            var current = latest?.Rank;
            var latestDay = latest?.Date ?? today;

            return new AsoTrackedKeywordDto
            {
                Id = row.Id,
                StoreAppId = row.StoreAppId,
                Store = row.Store,
                Keyword = row.Keyword,
                Country = row.Country,
                TrackCompetitors = row.TrackCompetitors,
                CompetitorIds = row.CompetitorIds ?? [],
                TrackedDate = row.TrackedDate,
                LastCheckedDate = row.LastCheckedDate,
                CurrentRank = current,
                Change1 = AsoScoring.RankChange(current, RankOn(latestDay.AddDays(-1))),
                Change7 = AsoScoring.RankChange(current, RankOn(latestDay.AddDays(-7))),
                Change30 = AsoScoring.RankChange(current, RankOn(latestDay.AddDays(-RecentDays))),
                Sparkline = [.. Enumerable.Range(0, RecentDays).Select(offset => RankOn(today.AddDays(offset - RecentDays + 1)))],
                Depth = latest?.Depth ?? depth,
                Popularity = metric?.Popularity,
                Difficulty = metric?.Difficulty,
                Opportunity = AsoScoring.Opportunity(metric?.Popularity, metric?.Difficulty),
            };
        })];
    }
}
