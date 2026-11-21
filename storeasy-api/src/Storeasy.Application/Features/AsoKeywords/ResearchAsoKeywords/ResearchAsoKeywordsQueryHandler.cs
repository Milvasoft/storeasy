using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;

/// <summary>
/// Handles <see cref="ResearchAsoKeywordsQuery"/>: resolves the store record, measures the keywords with
/// <see cref="IAsoKeywordResearcher"/> and marks the ones already tracked. 503 AsoSourceUnavailable when no store source
/// answered for any keyword.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="Researcher"></param>
/// <param name="TrackedRepository"></param>
public record ResearchAsoKeywordsQueryHandler(IStoreContentService ContentService,
                                              IAsoKeywordResearcher Researcher,
                                              IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository) : IInterceptable, IQueryHandler<ResearchAsoKeywordsQuery, List<AsoKeywordResearchDto>>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IAsoKeywordResearcher _researcher = Researcher;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;

    /// <inheritdoc/>
    public async Task<Response<List<AsoKeywordResearchDto>>> Handle(ResearchAsoKeywordsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<List<AsoKeywordResearchDto>>.Error(null, MessageKey.StoreAppNotFound);

        if (!_researcher.Supports(app.Store))
            return Response<List<AsoKeywordResearchDto>>.Error(null, MessageKey.AsoStoreNotSupported);

        var country = AsoCountries.Normalize(request.Country);
        var keywords = AsoExtensions.NormalizeKeywords(request.Keywords, country);

        if (keywords.Count == 0)
            return Response<List<AsoKeywordResearchDto>>.Error(null, MessageKey.AsoKeywordsInvalid);

        var items = await _researcher.ResearchAsync(app.Store, country, AsoCountries.LanguageOf(request.Language, country), keywords, app.ExternalId, cancellationToken);

        if (items.All(i => i.Metric.Popularity == null && !i.ResultsRead))
            return Response<List<AsoKeywordResearchDto>>.Error(null, MessageKey.AsoSourceUnavailable);

        var tracked = await _trackedRepository.TrackedKeywordSetAsync(app.StoreAppId, country, cancellationToken);

        return Response<List<AsoKeywordResearchDto>>.Success([.. items.Select(item => new AsoKeywordResearchDto
        {
            Keyword = item.Metric.Keyword,
            Store = app.Store,
            Country = country,
            Popularity = item.Metric.Popularity,
            PopularitySource = item.Metric.PopularitySource,
            Difficulty = item.Metric.Difficulty,
            Opportunity = AsoScoring.Opportunity(item.Metric.Popularity, item.Metric.Difficulty),
            CompetingApps = item.Metric.CompetingApps,
            CurrentRank = item.OwnRank,
            RankChecked = item.ResultsRead,
            Depth = item.Depth,
            Tracked = tracked.Contains(item.Metric.Keyword),
            TopCompetitors = [.. (item.Metric.TopCompetitors ?? []).Select(c => c.ToDto(app.ExternalId))],
            RelatedTerms = item.Metric.RelatedTerms ?? [],
            PopularityDate = item.Metric.PopularityDate,
            ResultsDate = item.Metric.ResultsDate,
        })]);
    }
}
