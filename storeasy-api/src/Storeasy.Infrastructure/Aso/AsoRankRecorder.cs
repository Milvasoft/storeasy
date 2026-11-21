using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Aso;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// <see cref="IAsoRankRecorder"/>: one store search per store, country and keyword shared by every organization tracking
/// it, one rank row per tracked keyword and UTC day, and the followed competitors' rows of the same search. A keyword whose
/// search failed is not marked checked, so a later run of the same day tries it again. The same searches refresh the
/// keyword's difficulty and top results.
/// </summary>
/// <param name="trackedRepository"></param>
/// <param name="rankRepository"></param>
/// <param name="competitorRankRepository"></param>
/// <param name="researcher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class AsoRankRecorder(IStoreasyRepositoryBase<AsoTrackedKeyword> trackedRepository,
                                    IStoreasyRepositoryBase<AsoKeywordRank> rankRepository,
                                    IStoreasyRepositoryBase<AsoCompetitorRank> competitorRankRepository,
                                    AsoKeywordResearcher researcher,
                                    AsoOptions options,
                                    TimeProvider timeProvider,
                                    ILogger<AsoRankRecorder> logger) : IAsoRankRecorder
{
    /// <summary>
    /// Competitors followed by default (the top results of the first check, other than the app itself).
    /// </summary>
    public const int DefaultCompetitorCount = 5;

    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = trackedRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordRank> _rankRepository = rankRepository;
    private readonly IStoreasyRepositoryBase<AsoCompetitorRank> _competitorRankRepository = competitorRankRepository;
    private readonly AsoKeywordResearcher _researcher = researcher;
    private readonly AsoOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<AsoRankRecorder> _logger = logger;

    /// <inheritdoc/>
    public async Task<int> RecordDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var dayStart = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var stores = _options.GooglePlayRanksEnabled ? new[] { StoreKind.Apple, StoreKind.Google } : [StoreKind.Apple];

        var due = (await _trackedRepository.GetAllAsync(t => t.IsActive
                                                            && stores.Contains(t.Store)
                                                            && (t.LastCheckedDate == null || t.LastCheckedDate < dayStart)
                                                            && !t.StoreApp.App.IsDeleted,
                                                       projection: t => new AsoTrackedKeyword
                                                       {
                                                           Id = t.Id,
                                                           OrganizationId = t.OrganizationId,
                                                           StoreAppId = t.StoreAppId,
                                                           Store = t.Store,
                                                           Keyword = t.Keyword,
                                                           Country = t.Country,
                                                           TrackCompetitors = t.TrackCompetitors,
                                                           CompetitorIds = t.CompetitorIds,
                                                           LastCheckedDate = t.LastCheckedDate,
                                                           StoreApp = new StoreApp { ExternalId = t.StoreApp.ExternalId },
                                                       },
                                                       cancellationToken: cancellationToken)).OrderBy(t => t.LastCheckedDate ?? DateTime.MinValue)
                                                                                             .ThenBy(t => t.Id)
                                                                                             .Take(Math.Max(1, _options.MaxRankChecksPerRun))
                                                                                             .ToList();

        if (due.Count == 0)
            return 0;

        // The store id is kept aside and the navigation dropped, so updating the rows never touches the store records.
        var ownIds = due.ToDictionary(t => t.Id, t => t.StoreApp?.ExternalId);

        due.ForEach(t => t.StoreApp = null);

        var searches = new List<(StoreKind Store, string Country, string Keyword, AsoSearchResult Result)>();

        foreach (var group in due.GroupBy(t => (t.Store, t.Country, t.Keyword)))
        {
            var result = await _researcher.SearchAsync(group.Key.Store, group.Key.Keyword, group.Key.Country, AsoCountries.Language(group.Key.Country), cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("ASO rank check of a {Store} keyword in {Country} failed; it is tried again in the next run.", group.Key.Store, group.Key.Country);
                continue;
            }

            searches.Add((group.Key.Store, group.Key.Country, group.Key.Keyword, result));
        }

        var checkedRows = due.Where(t => searches.Any(s => s.Store == t.Store && s.Country == t.Country && s.Keyword == t.Keyword)).ToList();

        if (checkedRows.Count == 0)
            return 0;

        var ids = checkedRows.Select(t => t.Id).ToList();
        var recordedToday = (await _rankRepository.GetAllAsync(r => ids.Contains(r.TrackedKeywordId) && r.Date == today,
                                                               projection: r => new AsoKeywordRank { Id = r.Id, TrackedKeywordId = r.TrackedKeywordId },
                                                               cancellationToken: cancellationToken)).Select(r => r.TrackedKeywordId).ToHashSet();

        var ranks = new List<AsoKeywordRank>();
        var competitorRanks = new List<AsoCompetitorRank>();

        foreach (var row in checkedRows)
        {
            var result = searches.First(s => s.Store == row.Store && s.Country == row.Country && s.Keyword == row.Keyword).Result;
            var ownId = ownIds[row.Id];
            var rank = ownId == null ? null : result.Apps.FirstOrDefault(a => string.Equals(a.Id, ownId, StringComparison.OrdinalIgnoreCase))?.Rank;

            row.LastCheckedDate = now;
            row.LastRank = rank;

            if (row.TrackCompetitors && (row.CompetitorIds == null || row.CompetitorIds.Count == 0))
                row.CompetitorIds = [.. result.Apps.Where(a => !string.Equals(a.Id, ownId, StringComparison.OrdinalIgnoreCase)).Take(DefaultCompetitorCount).Select(a => a.Id)];

            if (recordedToday.Contains(row.Id))
                continue;

            ranks.Add(new AsoKeywordRank
            {
                OrganizationId = row.OrganizationId,
                TrackedKeywordId = row.Id,
                StoreAppId = row.StoreAppId,
                Store = row.Store,
                Keyword = row.Keyword,
                Country = row.Country,
                Date = today,
                Rank = rank,
                Depth = result.Apps.Count,
                CheckedDate = now,
            });

            if (!row.TrackCompetitors)
                continue;

            competitorRanks.AddRange((row.CompetitorIds ?? []).Select(id =>
            {
                var app = result.Apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

                return new AsoCompetitorRank
                {
                    OrganizationId = row.OrganizationId,
                    TrackedKeywordId = row.Id,
                    Date = today,
                    CompetitorId = id,
                    CompetitorName = app?.Name is { Length: > AsoCompetitorRank.NameMaxLength } name ? name[..AsoCompetitorRank.NameMaxLength] : app?.Name,
                    Rank = app?.Rank,
                };
            }));
        }

        try
        {
            if (ranks.Count > 0)
                await _rankRepository.AddRangeAsync(ranks, cancellationToken);

            if (competitorRanks.Count > 0)
                await _competitorRankRepository.AddRangeAsync(competitorRanks, cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Another instance recorded the same day at the same moment; its rows stand.
            _logger.LogInformation("ASO ranks of today were recorded concurrently: {Error}.", exception.GetType().Name);
        }

        await _trackedRepository.UpdateAsync(checkedRows, cancellationToken, t => t.LastCheckedDate, t => t.LastRank, t => t.CompetitorIds);

        await _researcher.SaveResultsAsync(searches, cancellationToken);

        return checkedRows.Count;
    }
}
