using Storeasy.Domain.Aso;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// A store's search autocomplete (public, no account): the terms the store suggests for what was typed. Implementations
/// are polite (one request at a time per host, a pause between requests, a timeout, a cache) and never throw for a store
/// failure: they answer null and the caller degrades.
/// </summary>
public interface IAsoSuggestionSource
{
    /// <summary>
    /// Store the suggestions belong to.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Short source name kept with a measurement (e.g. <c>apple-search-hints</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the source may be called for <paramref name="country"/> (configuration and known storefronts).
    /// </summary>
    /// <param name="country">Lower case country code.</param>
    /// <returns></returns>
    bool Supports(string country);

    /// <summary>
    /// The suggestions for <paramref name="term"/> in the store's order, or null when the store could not be read.
    /// </summary>
    /// <param name="term"></param>
    /// <param name="country">Lower case country code.</param>
    /// <param name="language">Two letter language code.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<string>> SuggestAsync(string term, string country, string language, CancellationToken cancellationToken);
}

/// <summary>
/// A store's search results for a term (public, no account), in the store's order. Same politeness and failure rules as
/// <see cref="IAsoSuggestionSource"/>.
/// </summary>
public interface IAsoSearchSource
{
    /// <summary>
    /// Store searched.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Whether the source may be called (a source behind a configuration switch answers false while it is off).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The first <paramref name="depth"/> results, or null when the store could not be read.
    /// </summary>
    /// <param name="term"></param>
    /// <param name="country">Lower case country code.</param>
    /// <param name="language">Two letter language code.</param>
    /// <param name="depth"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoSearchResult> SearchAsync(string term, string country, string language, int depth, CancellationToken cancellationToken);
}

/// <summary>
/// An optional, account based popularity source (Apple Search Ads). Used before the autocomplete estimate when configured.
/// </summary>
public interface IAsoPopularityProvider
{
    /// <summary>
    /// Whether credentials are configured.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Source name kept with a measurement.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Popularity 0 to 100 of a keyword in a country, or null when unknown.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="keyword"></param>
    /// <param name="country"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int?> GetPopularityAsync(StoreKind store, string keyword, string country, CancellationToken cancellationToken);
}

/// <summary>
/// Measures keywords from the store sources: popularity, difficulty, competitors and whether an app ranks. Measurements are
/// kept in <see cref="AsoKeywordMetric"/> and reused while fresh.
/// </summary>
public interface IAsoKeywordResearcher
{
    /// <summary>
    /// Whether keyword research works for <paramref name="store"/> (Microsoft Store has no public search to read).
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    bool Supports(StoreKind store);

    /// <summary>
    /// Measures each keyword (normalized, unique) in one store and country. <paramref name="ownAppId"/> is the store id of
    /// the app whose rank is looked up in the results (App Store track id, Play package).
    /// </summary>
    /// <param name="store"></param>
    /// <param name="country">Lower case country code.</param>
    /// <param name="language">Two letter language code.</param>
    /// <param name="keywords"></param>
    /// <param name="ownAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<AsoResearchItem>> ResearchAsync(StoreKind store, string country, string language, IReadOnlyList<string> keywords, string ownAppId, CancellationToken cancellationToken);

    /// <summary>
    /// The search results of one term (cached), or null when the store could not be read or has no readable search.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="term"></param>
    /// <param name="country"></param>
    /// <param name="language"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoSearchResult> SearchAsync(StoreKind store, string term, string country, string language, CancellationToken cancellationToken);

    /// <summary>
    /// The store's search suggestions of one term (cached), normalized; empty when none or unavailable.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="term"></param>
    /// <param name="country"></param>
    /// <param name="language"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<string>> SuggestAsync(StoreKind store, string term, string country, string language, CancellationToken cancellationToken);
}

/// <summary>
/// Records the daily ranks of the tracked keywords (the rank job).
/// </summary>
public interface IAsoRankRecorder
{
    /// <summary>
    /// Checks the active tracked keywords not yet checked today (UTC), at most the configured number per run, one store
    /// search per store, country and keyword, and records their ranks and their competitors' ranks. Returns how many
    /// tracked keywords were recorded.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> RecordDueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads the daily rank series of a keyword: the query other features (such as a before/after impact report) use.
/// </summary>
public interface IAsoRankSeries
{
    /// <summary>
    /// One point per UTC day from <paramref name="from"/> to <paramref name="to"/> (both included) of the store record's
    /// rank for the keyword in the country. A day that was not checked has <see cref="AsoRankPoint.Checked"/> false; a
    /// checked day without a rank means the app was not in the results read.
    /// </summary>
    /// <param name="storeAppId"></param>
    /// <param name="keyword">Keyword in any form; it is normalized.</param>
    /// <param name="country">Country code in any case.</param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoRankSeries> GetRankSeriesAsync(int storeAppId, string keyword, string country, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <summary>
/// Search results of a term.
/// </summary>
/// <param name="ResultCount">How many results the store returned (up to the depth asked).</param>
/// <param name="Apps">The results in order (<see cref="AsoCompetitor.Rank"/> set).</param>
public sealed record AsoSearchResult(int ResultCount, IReadOnlyList<AsoCompetitor> Apps);

/// <summary>
/// One researched keyword.
/// </summary>
/// <param name="Metric">The measurement (saved, possibly reused while fresh).</param>
/// <param name="OwnRank">The app's position in the results, 1 based; null when not in them or not read.</param>
/// <param name="ResultsRead">Whether the search results were read now, so a null <paramref name="OwnRank"/> means "not ranked".</param>
/// <param name="Depth">How many results were read.</param>
public sealed record AsoResearchItem(AsoKeywordMetric Metric, int? OwnRank, bool ResultsRead, int Depth);

/// <summary>
/// One day of a rank series.
/// </summary>
/// <param name="Date">UTC day.</param>
/// <param name="Checked">Whether the rank was checked that day.</param>
/// <param name="Rank">Position, 1 based; null when not checked or not in the results read.</param>
public sealed record AsoRankPoint(DateOnly Date, bool Checked, int? Rank);

/// <summary>
/// A daily rank series.
/// </summary>
/// <param name="StoreAppId"></param>
/// <param name="Keyword">Normalized keyword.</param>
/// <param name="Country">Lower case country code.</param>
/// <param name="Points">One per day, oldest first.</param>
public sealed record AsoRankSeries(int StoreAppId, string Keyword, string Country, IReadOnlyList<AsoRankPoint> Points);
