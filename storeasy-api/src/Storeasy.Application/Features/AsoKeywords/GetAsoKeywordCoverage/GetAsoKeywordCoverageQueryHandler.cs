using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordCoverage;

/// <summary>
/// Handles <see cref="GetAsoKeywordCoverageQuery"/>: reads the App Store record's texts live and runs
/// <see cref="KeywordCoverage"/> per locale against every active tracked keyword of the record (all countries).
/// </summary>
/// <param name="ContentService"></param>
/// <param name="TrackedRepository"></param>
public record GetAsoKeywordCoverageQueryHandler(IStoreContentService ContentService,
                                                IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository) : IInterceptable, IQueryHandler<GetAsoKeywordCoverageQuery, List<AsoKeywordCoverageDto>>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;

    /// <inheritdoc/>
    public async Task<Response<List<AsoKeywordCoverageDto>>> Handle(GetAsoKeywordCoverageQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, StoreKind.Apple, cancellationToken);

        if (app == null)
            return Response<List<AsoKeywordCoverageDto>>.Error(null, MessageKey.StoreAppNotFound);

        var read = await _contentService.ReadContentAsync(app, ContentParts.Texts, null, StoreKind.Apple, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<List<AsoKeywordCoverageDto>>();

        var tracked = (await _trackedRepository.GetAllAsync(t => t.StoreAppId == app.StoreAppId && t.IsActive,
                                                            projection: t => new AsoTrackedKeyword { Id = t.Id, Keyword = t.Keyword },
                                                            cancellationToken: cancellationToken)).Select(t => t.Keyword).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToList();

        var snapshot = read.Snapshot;
        var locales = (snapshot.Locales ?? []).Where(l => string.IsNullOrWhiteSpace(request.Locale) || string.Equals(l, request.Locale, StringComparison.OrdinalIgnoreCase))
                                              .OrderBy(l => l == snapshot.PrimaryLocale ? 0 : 1)
                                              .ThenBy(l => l, StringComparer.Ordinal);

        return Response<List<AsoKeywordCoverageDto>>.Success([.. locales.Select(locale =>
        {
            var texts = snapshot.ListingTexts(locale);
            var result = KeywordCoverage.Analyze(texts.Name, texts.Subtitle, texts.Keywords, tracked, AsoText.Culture(locale));

            return new AsoKeywordCoverageDto
            {
                Locale = locale,
                Name = texts.Name,
                Subtitle = texts.Subtitle,
                Keywords = texts.Keywords,
                KeywordsLength = result.KeywordsLength,
                Remaining = result.Remaining,
                CharactersNeeded = result.CharactersNeeded,
                Items = [.. result.Items.Select(i => new AsoKeywordCoverageItemDto { Keyword = i.Keyword, Covered = i.Covered, MissingWords = [.. i.MissingWords], Fields = [.. i.Fields] })],
                RedundantWords = [.. result.RedundantWords],
                MissingWords = [.. result.MissingWords],
            };
        })]);
    }
}
