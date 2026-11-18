using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Artifacts.GetArtifactCounts;

/// <summary>
/// Handles <see cref="GetArtifactCountsQuery"/>: one projection of status and source in the period, counted in memory.
/// </summary>
/// <param name="sessionRepository"></param>
/// <param name="timeProvider"></param>
public class GetArtifactCountsQueryHandler(IStoreasyRepositoryBase<ArtifactSession> sessionRepository, TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetArtifactCountsQuery, ArtifactCountsDto>
{
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<ArtifactCountsDto>> Handle(GetArtifactCountsQuery request, CancellationToken cancellationToken)
    {
        var to = request.To?.ToUniversalTime() ?? _timeProvider.GetUtcNow().UtcDateTime;
        var from = request.From?.ToUniversalTime() ?? to.AddDays(-30);

        if (to - from > TimeSpan.FromDays(GetArtifactCountsQuery.MaxDays))
            from = to.AddDays(-GetArtifactCountsQuery.MaxDays);

        var sessions = await _sessionRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId
                                                                 && (request.AppId == null || s.AppId == request.AppId)
                                                                 && s.StartDate >= from
                                                                 && s.StartDate < to,
                                                            projection: s => new ArtifactSession { Status = s.Status, Source = s.Source },
                                                            cancellationToken: cancellationToken);

        return Response<ArtifactCountsDto>.Success(new ArtifactCountsDto
        {
            From = from,
            To = to,
            Total = sessions.Count,
            ByStatus = [.. Enum.GetValues<ArtifactSessionStatus>().Select(status => new ArtifactStatusCountDto { Status = status, Count = sessions.Count(s => s.Status == status) })],
            BySource = [.. sessions.GroupBy(s => s.Source).OrderBy(g => g.Key).Select(g => new ArtifactSourceCountDto { Source = g.Key, Count = g.Count() })],
        });
    }
}
