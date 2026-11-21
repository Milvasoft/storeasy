using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordSeeds;

/// <summary>
/// Handles <see cref="GetAsoKeywordSeedsQuery"/>: listing seeds (read live; a failed read becomes a warning and the other
/// sources still answer), competitor name seeds from one search of the first listing seed, and the store's suggestions for
/// the first two listing seeds. At most four store calls, all cached.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="Researcher"></param>
/// <param name="TrackedRepository"></param>
/// <param name="Localizer"></param>
public record GetAsoKeywordSeedsQueryHandler(IStoreContentService ContentService,
                                             IAsoKeywordResearcher Researcher,
                                             IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                             IMilvaLocalizer Localizer) : IInterceptable, IQueryHandler<GetAsoKeywordSeedsQuery, AsoKeywordSeedsDto>
{
    private const int _suggestionSeeds = 2;

    private readonly IStoreContentService _contentService = ContentService;
    private readonly IAsoKeywordResearcher _researcher = Researcher;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<AsoKeywordSeedsDto>> Handle(GetAsoKeywordSeedsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.StoreAppNotFound);

        if (!_researcher.Supports(app.Store))
            return Response<AsoKeywordSeedsDto>.Error(null, MessageKey.AsoStoreNotSupported);

        var country = AsoCountries.Normalize(request.Country);
        var language = AsoCountries.Language(country);
        var culture = AsoExtensions.KeywordCulture(country);
        var (locale, texts, errorKey) = await _contentService.ReadAsoListingAsync(app, request.Locale, country, cancellationToken);

        var seeds = AsoSeeds.FromListing(texts.Name ?? (errorKey == null ? null : app.AppName), texts.Subtitle, texts.Keywords, texts.Description, culture);
        var seen = seeds.Select(s => s.Term).ToHashSet(StringComparer.Ordinal);

        void Add(IEnumerable<string> terms, string source)
        {
            foreach (var term in terms.Select(t => AsoText.Normalize(t, culture)).Where(t => t != null && t.Length <= AsoTrackedKeyword.KeywordMaxLength))
            {
                if (seen.Add(term))
                    seeds.Add((term, source));
            }
        }

        var listingSeeds = seeds.Select(s => s.Term).ToList();

        if (listingSeeds.Count > 0)
        {
            var search = await _researcher.SearchAsync(app.Store, listingSeeds[0], country, language, cancellationToken);
            var names = (search?.Apps ?? []).Where(a => !string.Equals(a.Id, app.ExternalId, StringComparison.OrdinalIgnoreCase)).Take(AsoScoring.TopCount).Select(a => a.Name);

            Add(AsoSeeds.FromCompetitorNames(names, culture), AsoSeeds.CompetitorSource);
        }

        foreach (var seed in listingSeeds.Take(_suggestionSeeds))
            Add(await _researcher.SuggestAsync(app.Store, seed, country, language, cancellationToken), AsoSeeds.SuggestionSource);

        var tracked = await _trackedRepository.TrackedKeywordSetAsync(app.StoreAppId, country, cancellationToken);

        return Response<AsoKeywordSeedsDto>.Success(new AsoKeywordSeedsDto
        {
            Store = app.Store,
            Country = country,
            Locale = locale,
            Warning = errorKey == null ? null : _localizer[MessageKey.AsoListingUnavailable],
            Seeds = [.. seeds.Take(GetAsoKeywordSeedsQuery.MaxSeeds).Select(s => new AsoKeywordSeedDto { Term = s.Term, Source = s.Source, Tracked = tracked.Contains(s.Term) })],
        });
    }
}
