using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordRanks;

/// <summary>
/// Handles <see cref="GetAsoKeywordRanksQuery"/>: the keyword of the organization (404 AsoTrackedKeywordNotFound
/// otherwise), its series through <see cref="IAsoRankSeries"/>, the competitors' rows of the same days and the latest
/// measurement's top results.
/// </summary>
/// <param name="TrackedRepository"></param>
/// <param name="RankRepository"></param>
/// <param name="CompetitorRankRepository"></param>
/// <param name="MetricRepository"></param>
/// <param name="RankSeries"></param>
/// <param name="ContentService"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record GetAsoKeywordRanksQueryHandler(IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                             IStoreasyRepositoryBase<AsoKeywordRank> RankRepository,
                                             IStoreasyRepositoryBase<AsoCompetitorRank> CompetitorRankRepository,
                                             IStoreasyRepositoryBase<AsoKeywordMetric> MetricRepository,
                                             IAsoRankSeries RankSeries,
                                             IStoreContentService ContentService,
                                             AsoOptions Options,
                                             TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoKeywordRanksQuery, AsoKeywordRanksDto>
{
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordRank> _rankRepository = RankRepository;
    private readonly IStoreasyRepositoryBase<AsoCompetitorRank> _competitorRankRepository = CompetitorRankRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordMetric> _metricRepository = MetricRepository;
    private readonly IAsoRankSeries _rankSeries = RankSeries;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly AsoOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoKeywordRanksDto>> Handle(GetAsoKeywordRanksQuery request, CancellationToken cancellationToken)
    {
        var row = await _trackedRepository.GetFirstOrDefaultAsync(t => t.Id == request.Id && t.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (row == null)
            return Response<AsoKeywordRanksDto>.Error(null, MessageKey.AsoTrackedKeywordNotFound);

        var to = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var from = to.AddDays(1 - request.Days);
        var series = await _rankSeries.GetRankSeriesAsync(row.StoreAppId, row.Keyword, row.Country, from, to, cancellationToken);
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, row.StoreAppId, cancellationToken);

        var competitorRows = await _competitorRankRepository.GetAllAsync(c => c.TrackedKeywordId == row.Id && c.Date >= from && c.Date <= to,
                                                                         projection: c => new AsoCompetitorRank { CompetitorId = c.CompetitorId, CompetitorName = c.CompetitorName, Date = c.Date, Rank = c.Rank },
                                                                         cancellationToken: cancellationToken);

        var metric = await _metricRepository.GetFirstOrDefaultAsync(m => m.Store == row.Store && m.Country == row.Country && m.Keyword == row.Keyword,
                                                                    projection: m => new AsoKeywordMetric { Id = m.Id, TopCompetitors = m.TopCompetitors, ResultsDate = m.ResultsDate },
                                                                    cancellationToken: cancellationToken);

        var keyword = (await new List<AsoTrackedKeyword> { row }.ToDtosAsync(_rankRepository, _metricRepository, to, _options.RankDepth, cancellationToken))[0];
        var days = series.Points.Select(p => p.Date).ToList();

        return Response<AsoKeywordRanksDto>.Success(new AsoKeywordRanksDto
        {
            Keyword = keyword,
            From = from,
            To = to,
            Points = [.. series.Points.Select(p => new AsoRankPointDto { Date = p.Date, Checked = p.Checked, Rank = p.Rank })],
            Competitors = [.. competitorRows.GroupBy(c => c.CompetitorId).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            {
                var byDay = g.ToDictionary(c => c.Date);

                return new AsoCompetitorSeriesDto
                {
                    Id = g.Key,
                    Name = g.OrderByDescending(c => c.Date).Select(c => c.CompetitorName).FirstOrDefault(n => n != null),
                    Points = [.. days.Select(d => new AsoRankPointDto { Date = d, Checked = byDay.ContainsKey(d), Rank = byDay.TryGetValue(d, out var c) ? c.Rank : null })],
                };
            })],
            TopCompetitors = [.. (metric?.TopCompetitors ?? []).Select(c => c.ToDto(app?.ExternalId))],
            ResultsDate = metric?.ResultsDate,
        });
    }
}
