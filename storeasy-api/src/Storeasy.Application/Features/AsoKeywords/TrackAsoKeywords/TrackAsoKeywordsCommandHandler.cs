using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoDtos;
using Storeasy.Application.Utils.Aso;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.TrackAsoKeywords;

/// <summary>
/// Handles <see cref="TrackAsoKeywordsCommand"/>: checks the store, the organization's limit of active keywords, adds new
/// rows and puts untracked ones back (their history continues), updates the competitor choice of already tracked ones and
/// writes one audit entry. Returns every active keyword of the store record.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="TrackedRepository"></param>
/// <param name="RankRepository"></param>
/// <param name="MetricRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="Localizer"></param>
/// <param name="Options"></param>
/// <param name="TimeProvider"></param>
public record TrackAsoKeywordsCommandHandler(IStoreContentService ContentService,
                                             IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                             IStoreasyRepositoryBase<AsoKeywordRank> RankRepository,
                                             IStoreasyRepositoryBase<AsoKeywordMetric> MetricRepository,
                                             IAuditTrail AuditTrail,
                                             ICurrentUser CurrentUser,
                                             IMilvaLocalizer Localizer,
                                             AsoOptions Options,
                                             TimeProvider TimeProvider) : IInterceptable, ICommandHandler<TrackAsoKeywordsCommand, List<AsoTrackedKeywordDto>>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordRank> _rankRepository = RankRepository;
    private readonly IStoreasyRepositoryBase<AsoKeywordMetric> _metricRepository = MetricRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly AsoOptions _options = Options;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<AsoTrackedKeywordDto>>> Handle(TrackAsoKeywordsCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppOfAppAsync(request.OrganizationId, request.AppId, request.Store, cancellationToken);

        if (app == null)
            return Response<List<AsoTrackedKeywordDto>>.Error(null, MessageKey.StoreAppNotFound);

        if (app.Store == StoreKind.Microsoft)
            return Response<List<AsoTrackedKeywordDto>>.Error(null, MessageKey.AsoStoreNotSupported);

        if (app.Store == StoreKind.Google && !_options.GooglePlayRanksEnabled)
            return Response<List<AsoTrackedKeywordDto>>.Error(null, MessageKey.AsoGooglePlayRanksDisabled);

        var country = AsoCountries.Normalize(request.Country);
        var keywords = AsoExtensions.NormalizeKeywords(request.Keywords, country);

        if (keywords.Count == 0)
            return Response<List<AsoTrackedKeywordDto>>.Error(null, MessageKey.AsoKeywordsInvalid);

        var competitorIds = (request.CompetitorIds ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).Take(AsoTrackedKeyword.MaxCompetitors).ToList();

        var existing = await _trackedRepository.GetAllAsync(t => t.StoreAppId == app.StoreAppId && t.Country == country && keywords.Contains(t.Keyword),
                                                            cancellationToken: cancellationToken);

        var activating = keywords.Count(k => !existing.Any(e => e.Keyword == k && e.IsActive));

        if (activating > 0)
        {
            var active = await _trackedRepository.GetAllAsync(t => t.OrganizationId == request.OrganizationId && t.IsActive,
                                                              projection: t => new AsoTrackedKeyword { Id = t.Id },
                                                              cancellationToken: cancellationToken);

            if (active.Count + activating > _options.MaxTrackedKeywordsPerOrganization)
                return Response<List<AsoTrackedKeywordDto>>.Error(null, new ResponseMessage(MessageKey.AsoKeywordLimitReached, _localizer[MessageKey.AsoKeywordLimitReached, _options.MaxTrackedKeywordsPerOrganization]));
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var row in existing)
        {
            if (!row.IsActive)
            {
                row.IsActive = true;
                row.TrackedDate = now;
                row.UntrackedDate = null;
            }

            row.TrackCompetitors = request.TrackCompetitors;

            if (competitorIds.Count > 0 || !request.TrackCompetitors)
                row.CompetitorIds = request.TrackCompetitors ? competitorIds : [];
        }

        if (existing.Count > 0)
            await _trackedRepository.UpdateAsync(existing, cancellationToken, t => t.IsActive, t => t.TrackedDate, t => t.UntrackedDate, t => t.TrackCompetitors, t => t.CompetitorIds);

        var added = keywords.Where(k => !existing.Any(e => e.Keyword == k)).Select(k => new AsoTrackedKeyword
        {
            OrganizationId = request.OrganizationId,
            AppId = app.AppId,
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            Keyword = k,
            Country = country,
            TrackCompetitors = request.TrackCompetitors,
            CompetitorIds = request.TrackCompetitors ? competitorIds : [],
            IsActive = true,
            TrackedDate = now,
            CreationDate = now,
            CreatorUserName = _currentUser.UserName,
        }).ToList();

        if (added.Count > 0)
            await _trackedRepository.AddRangeAsync(added, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AsoKeywordsTracked,
                                     app.StoreAppId.ToString(),
                                     $"{keywords.Count} keywords tracked for {app.AppName} ({app.Store}, {country}).",
                                     app.Store,
                                     details: new { Country = country, Keywords = keywords, request.TrackCompetitors, CompetitorIds = competitorIds },
                                     cancellationToken: cancellationToken);

        var rows = await _trackedRepository.GetAllAsync(t => t.StoreAppId == app.StoreAppId && t.IsActive, cancellationToken: cancellationToken);

        return Response<List<AsoTrackedKeywordDto>>.Success(await rows.OrderBy(r => r.Country).ThenBy(r => r.Keyword).ToList().ToDtosAsync(_rankRepository, _metricRepository, DateOnly.FromDateTime(now), _options.RankDepth, cancellationToken));
    }
}
