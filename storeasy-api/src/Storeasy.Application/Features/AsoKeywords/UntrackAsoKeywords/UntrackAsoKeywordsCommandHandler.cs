using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Domain.Aso;

namespace Storeasy.Application.Features.AsoKeywords.UntrackAsoKeywords;

/// <summary>
/// Handles <see cref="UntrackAsoKeywordsCommand"/>: marks the organization's active rows inactive in one update and writes
/// one audit entry per store record.
/// </summary>
/// <param name="TrackedRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record UntrackAsoKeywordsCommandHandler(IStoreasyRepositoryBase<AsoTrackedKeyword> TrackedRepository,
                                               IAuditTrail AuditTrail,
                                               TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UntrackAsoKeywordsCommand, int>
{
    private readonly IStoreasyRepositoryBase<AsoTrackedKeyword> _trackedRepository = TrackedRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UntrackAsoKeywordsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Distinct().ToList();
        var rows = await _trackedRepository.GetAllAsync(t => ids.Contains(t.Id) && t.OrganizationId == request.OrganizationId && t.IsActive,
                                                        projection: t => new AsoTrackedKeyword { Id = t.Id, StoreAppId = t.StoreAppId, Store = t.Store, Keyword = t.Keyword, Country = t.Country },
                                                        cancellationToken: cancellationToken);

        if (rows.Count == 0)
            return Response<int>.Error(0, MessageKey.AsoTrackedKeywordNotFound);

        var found = rows.Select(r => r.Id).ToList();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _trackedRepository.ExecuteUpdateAsync(t => found.Contains(t.Id),
                                                    new SetPropertyBuilder<AsoTrackedKeyword>().SetPropertyValue(t => t.IsActive, false)
                                                                                                .SetPropertyValue(t => t.UntrackedDate, now),
                                                    cancellationToken: cancellationToken);

        await _auditTrail.WriteManyAsync([.. rows.GroupBy(r => (r.StoreAppId, r.Store)).Select(g => new AuditEntry(request.OrganizationId,
                                                                                                                  AuditAction.AsoKeywordsUntracked,
                                                                                                                  g.Key.StoreAppId.ToString(),
                                                                                                                  $"{g.Count()} keywords untracked for store record {g.Key.StoreAppId}.",
                                                                                                                  g.Key.Store,
                                                                                                                  new { Keywords = g.Select(r => $"{r.Keyword} ({r.Country})").ToList() }))],
                                         cancellationToken);

        return Response<int>.Success(rows.Count);
    }
}
