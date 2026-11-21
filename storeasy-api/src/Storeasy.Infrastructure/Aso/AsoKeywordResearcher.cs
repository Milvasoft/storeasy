using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// <see cref="IAsoKeywordResearcher"/> over the store sources. Popularity: the configured account source when there is one,
/// else the store's autocomplete (<see cref="AsoScoring.MeasurePopularityAsync"/>). Difficulty, competitors and the app's
/// rank: the store's search results (<see cref="AsoScoring.Difficulty"/>). A saved measurement younger than
/// <see cref="AsoOptions.FreshnessHours"/> is reused; a source that fails leaves the older value in place.
/// </summary>
/// <param name="suggestionSources"></param>
/// <param name="searchSources"></param>
/// <param name="popularityProvider"></param>
/// <param name="metricRepository"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class AsoKeywordResearcher(IEnumerable<IAsoSuggestionSource> suggestionSources,
                                         IEnumerable<IAsoSearchSource> searchSources,
                                         IAsoPopularityProvider popularityProvider,
                                         IStoreasyRepositoryBase<AsoKeywordMetric> metricRepository,
                                         AsoOptions options,
                                         TimeProvider timeProvider,
                                         ILogger<AsoKeywordResearcher> logger) : IAsoKeywordResearcher
{
    private readonly List<IAsoSuggestionSource> _suggestionSources = [.. suggestionSources];
    private readonly List<IAsoSearchSource> _searchSources = [.. searchSources];
    private readonly IAsoPopularityProvider _popularityProvider = popularityProvider;
    private readonly IStoreasyRepositoryBase<AsoKeywordMetric> _metricRepository = metricRepository;
    private readonly AsoOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<AsoKeywordResearcher> _logger = logger;

    /// <inheritdoc/>
    public bool Supports(StoreKind store) => store is StoreKind.Apple or StoreKind.Google;

    /// <inheritdoc/>
    public async Task<List<AsoResearchItem>> ResearchAsync(StoreKind store, string country, string language, IReadOnlyList<string> keywords, string ownAppId, CancellationToken cancellationToken)
    {
        var metrics = await LoadAsync(store, country, keywords, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var fresh = now.AddHours(-Math.Max(1, _options.FreshnessHours));
        var suggestionSource = _suggestionSources.FirstOrDefault(s => s.Store == store && s.Supports(country));
        var items = new List<AsoResearchItem>();

        foreach (var keyword in keywords)
        {
            var metric = metrics[keyword];

            if (metric.Popularity == null || metric.PopularityDate == null || metric.PopularityDate < fresh)
                await MeasurePopularityAsync(metric, suggestionSource, language, now, cancellationToken);

            var results = await SearchAsync(store, keyword, country, language, cancellationToken);

            if (results != null)
                Apply(metric, results, now);

            var ownRank = results == null || ownAppId == null ? null : results.Apps.FirstOrDefault(a => string.Equals(a.Id, ownAppId, StringComparison.OrdinalIgnoreCase))?.Rank;

            items.Add(new AsoResearchItem(metric, ownRank, results != null, results?.ResultCount ?? 0));
        }

        await SaveAsync([.. metrics.Values], cancellationToken);

        return items;
    }

    /// <inheritdoc/>
    public async Task<AsoSearchResult> SearchAsync(StoreKind store, string term, string country, string language, CancellationToken cancellationToken)
    {
        var source = _searchSources.FirstOrDefault(s => s.Store == store && s.IsEnabled);

        return source == null ? null : await source.SearchAsync(term, country, language, Math.Clamp(_options.RankDepth, 1, ITunesSearchSource.MaxResults), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<string>> SuggestAsync(StoreKind store, string term, string country, string language, CancellationToken cancellationToken)
    {
        var source = _suggestionSources.FirstOrDefault(s => s.Store == store && s.Supports(country));

        if (source == null)
            return [];

        var culture = AsoExtensions.KeywordCulture(country);
        var hints = await source.SuggestAsync(term, country, language, cancellationToken);

        return [.. (hints ?? []).Select(h => AsoText.Normalize(h, culture)).Where(h => h != null).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Saves the measurements of search results read elsewhere (the rank job reads the same results): competing apps,
    /// difficulty and the top ten of each keyword. Popularity is left as it is.
    /// </summary>
    /// <param name="results">Results by store, country and normalized keyword.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SaveResultsAsync(IReadOnlyList<(StoreKind Store, string Country, string Keyword, AsoSearchResult Result)> results, CancellationToken cancellationToken)
    {
        if (results.Count == 0)
            return;

        var keywords = results.Select(r => r.Keyword).Distinct(StringComparer.Ordinal).ToList();
        var saved = await _metricRepository.GetAllAsync(m => keywords.Contains(m.Keyword), cancellationToken: cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var metrics = new List<AsoKeywordMetric>();

        foreach (var (store, country, keyword, result) in results)
        {
            var metric = saved.FirstOrDefault(m => m.Store == store && m.Country == country && m.Keyword == keyword) ?? new AsoKeywordMetric { Store = store, Country = country, Keyword = keyword };

            Apply(metric, result, now);
            metrics.Add(metric);
        }

        await SaveAsync(metrics, cancellationToken);
    }

    private async Task MeasurePopularityAsync(AsoKeywordMetric metric, IAsoSuggestionSource source, string language, DateTime now, CancellationToken cancellationToken)
    {
        if (_popularityProvider.IsConfigured && await _popularityProvider.GetPopularityAsync(metric.Store, metric.Keyword, metric.Country, cancellationToken) is { } premium)
        {
            metric.Popularity = Math.Clamp(premium, 0, 100);
            metric.PopularitySource = _popularityProvider.Name;
            metric.PopularityDate = now;

            return;
        }

        if (source == null)
            return;

        var culture = AsoExtensions.KeywordCulture(metric.Country);

        async Task<IReadOnlyList<string>> Suggest(string prefix)
        {
            var hints = await source.SuggestAsync(prefix, metric.Country, language, cancellationToken);

            return hints == null ? null : [.. hints.Select(h => AsoText.Normalize(h, culture)).Where(h => h != null)];
        }

        var measurement = await AsoScoring.MeasurePopularityAsync(metric.Keyword, _options.MaxPrefixQueries, Suggest);

        if (measurement.Popularity == null)
            return;

        metric.Popularity = measurement.Popularity;
        metric.PopularitySource = source.Name;
        metric.PopularityDate = now;
        metric.RelatedTerms = [.. measurement.RelatedTerms.Take(20)];
    }

    private static void Apply(AsoKeywordMetric metric, AsoSearchResult results, DateTime now)
    {
        var culture = AsoExtensions.KeywordCulture(metric.Country);

        metric.CompetingApps = results.ResultCount;
        metric.TopCompetitors = [.. results.Apps.Take(AsoScoring.TopCount).Select(a => new AsoCompetitor
        {
            Rank = a.Rank,
            Id = a.Id,
            Name = Cut(a.Name, AsoCompetitorRank.NameMaxLength),
            Developer = Cut(a.Developer, AsoCompetitorRank.NameMaxLength),
            Rating = a.Rating,
            RatingCount = a.RatingCount,
            IconUrl = a.IconUrl,
        })];
        metric.Difficulty = AsoScoring.Difficulty(results.Apps, results.ResultCount, metric.Keyword, culture);
        metric.ResultsDate = now;
    }

    private async Task<Dictionary<string, AsoKeywordMetric>> LoadAsync(StoreKind store, string country, IReadOnlyList<string> keywords, CancellationToken cancellationToken)
    {
        var list = keywords.ToList();
        var saved = await _metricRepository.GetAllAsync(m => m.Store == store && m.Country == country && list.Contains(m.Keyword), cancellationToken: cancellationToken);

        return list.Distinct(StringComparer.Ordinal).ToDictionary(k => k, k => saved.FirstOrDefault(m => m.Keyword == k) ?? new AsoKeywordMetric { Store = store, Country = country, Keyword = k }, StringComparer.Ordinal);
    }

    private async Task SaveAsync(List<AsoKeywordMetric> metrics, CancellationToken cancellationToken)
    {
        var added = metrics.Where(m => m.Id == 0).ToList();
        var updated = metrics.Where(m => m.Id != 0).ToList();

        try
        {
            if (updated.Count > 0)
            {
                await _metricRepository.UpdateAsync(updated,
                                                    cancellationToken,
                                                    m => m.Popularity,
                                                    m => m.PopularitySource,
                                                    m => m.PopularityDate,
                                                    m => m.RelatedTerms,
                                                    m => m.Difficulty,
                                                    m => m.CompetingApps,
                                                    m => m.TopCompetitors,
                                                    m => m.ResultsDate);
            }

            if (added.Count > 0)
                await _metricRepository.AddRangeAsync(added, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Another research saved the same keyword at the same moment; its measurement is as good as this one.
            _logger.LogInformation("ASO measurements were saved concurrently and this copy was dropped: {Error}.", exception.GetType().Name);
        }
    }

    private static string Cut(string value, int maxLength) => value == null || value.Length <= maxLength ? value : value[..maxLength];
}
