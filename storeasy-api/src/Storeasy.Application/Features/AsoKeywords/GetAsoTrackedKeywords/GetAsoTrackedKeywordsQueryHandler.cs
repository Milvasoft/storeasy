using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoTrackedKeywords;

/// <summary>
/// Handles <see cref="GetAsoTrackedKeywordsQuery"/>. The rows are found by the app's current store record, so a record
/// moved to another app keeps its keywords. An app of another organization, or without a record in the store, gives an
/// empty list.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="TrackedRepository"></param>
/// <param name="RankRepository"></param>
/// <param name="MetricRepository"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record GetAsoTrackedKeywordsQueryHandler(IStoreContentService ContentService,
                                                IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                                IStoreasyRepositoryBase<AsoKeywordRank> RankRepository,
                                                IStoreasyRepositoryBase<AsoKeywordMetric> MetricRepository,
                                                AsoOptions Options,
                                                TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoTrackedKeywordsQuery, List<AsoTrackedKeywordDto>>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordRank> _rankRepository = RankRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordMetric> _metricRepository = MetricRepository;
    private readonly AsoOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<AsoTrackedKeywordDto>>> Handle(GetAsoTrackedKeywordsQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<List<AsoTrackedKeywordDto>>.Success([]);

        var country = AsoCountries.Normalize(request.Country);
        var rows = await _trackedRepository.GetAllAsync(t => t.OrganizationId == request.OrganizationId
                                                             && t.StoreAppId == app.StoreAppId
                                                             && t.IsActive
                                                             && (country == null || t.Country == country),
                                                        cancellationToken: cancellationToken);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return Response<List<AsoTrackedKeywordDto>>.Success(await rows.OrderBy(r => r.Country).ThenBy(r => r.Keyword).ToList().ToDtosAsync(_rankRepository, _metricRepository, today, _options.RankDepth, cancellationToken));
    }
}
