using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Artifacts.GetArtifactSummary;

/// <summary>
/// Handles <see cref="GetArtifactSummaryQuery"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="SessionRepository"></param>
/// <param name="RuleRepository"></param>
public record GetArtifactSummaryQueryHandler(IStoreasyRepositoryBase<App> AppRepository,
                                             IStoreasyRepositoryBase<ArtifactSession> SessionRepository,
                                             IStoreasyRepositoryBase<DeliveryRule> RuleRepository) : IInterceptable, IQueryHandler<GetArtifactSummaryQuery, List<ArtifactAppSummaryDto>>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = SessionRepository;
    private readonly IStoreasyRepositoryBase<DeliveryRule> _ruleRepository = RuleRepository;

    /// <inheritdoc/>
    public async Task<Response<List<ArtifactAppSummaryDto>>> Handle(GetArtifactSummaryQuery request, CancellationToken cancellationToken)
    {
        var apps = await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                    projection: a => new App { Id = a.Id, Name = a.Name },
                                                    cancellationToken: cancellationToken);

        var lastIds = await _sessionRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId && s.Status != ArtifactSessionStatus.Duplicate,
                                                           projection: s => new ArtifactSession { Id = s.Id, AppId = s.AppId },
                                                           cancellationToken: cancellationToken);

        var latest = lastIds.GroupBy(s => s.AppId).Select(g => g.Max(s => s.Id)).ToList();

        var sessions = await _sessionRepository.GetAllAsync(s => latest.Contains(s.Id), projection: ArtifactSessionListDto.Projection, cancellationToken: cancellationToken);

        var rules = await _ruleRepository.GetAllAsync(r => r.OrganizationId == request.OrganizationId, projection: DeliveryRuleDto.EntityProjection, cancellationToken: cancellationToken);

        return Response<List<ArtifactAppSummaryDto>>.Success([.. apps.OrderBy(a => a.Name).Select(a => new ArtifactAppSummaryDto
        {
            AppId = a.Id,
            AppName = a.Name,
            LastSession = sessions.FirstOrDefault(s => s.AppId == a.Id),
            Rules = [.. rules.Where(r => r.AppId == a.Id).Select(DeliveryRuleDto.From)],
        })]);
    }
}
