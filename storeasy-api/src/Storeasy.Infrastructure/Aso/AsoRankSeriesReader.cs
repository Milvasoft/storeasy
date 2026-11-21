using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Domain.Aso;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// <see cref="IAsoRankSeries"/> over the saved daily ranks. The rows are found by store record, keyword and country (not by
/// tracking row), so a keyword untracked and tracked again reads as one series.
/// </summary>
/// <param name="rankRepository"></param>
public sealed class AsoRankSeriesReader(IStoreasyRepositoryBase<AsoKeywordRank> rankRepository) : IAsoRankSeries
{
    private readonly IStoreasyRepositoryBase<AsoKeywordRank> _rankRepository = rankRepository;

    /// <inheritdoc/>
    public async Task<AsoRankSeries> GetRankSeriesAsync(int storeAppId, string keyword, string country, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var normalizedCountry = AsoCountries.Normalize(country);
        var normalizedKeyword = normalizedCountry == null ? null : AsoText.Normalize(keyword, AsoExtensions.KeywordCulture(normalizedCountry));

        if (normalizedKeyword == null || to < from)
            return new AsoRankSeries(storeAppId, normalizedKeyword, normalizedCountry, []);

        var rows = await _rankRepository.GetAllAsync(r => r.StoreAppId == storeAppId && r.Keyword == normalizedKeyword && r.Country == normalizedCountry && r.Date >= from && r.Date <= to,
                                                     projection: r => new AsoKeywordRank { Date = r.Date, Rank = r.Rank },
                                                     cancellationToken: cancellationToken);

        // One row per tracking row and day; if a keyword was ever tracked twice for the same record, the best rank of the day wins.
        var byDay = rows.GroupBy(r => r.Date).ToDictionary(g => g.Key, g => g.Min(r => r.Rank ?? int.MaxValue));
        var days = to.DayNumber - from.DayNumber + 1;

        return new AsoRankSeries(storeAppId,
                                 normalizedKeyword,
                                 normalizedCountry,
                                 [.. Enumerable.Range(0, days).Select(offset =>
                                 {
                                     var day = from.AddDays(offset);

                                     return byDay.TryGetValue(day, out var best) ? new AsoRankPoint(day, true, best == int.MaxValue ? null : best) : new AsoRankPoint(day, false, null);
                                 })]);
    }
}
