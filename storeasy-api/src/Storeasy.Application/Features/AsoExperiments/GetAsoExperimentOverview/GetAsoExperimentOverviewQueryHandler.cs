using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentOverview;

/// <summary>
/// Handles <see cref="GetAsoExperimentOverviewQuery"/>. Nothing is read from the stores; the records show the last sync.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="ContentService"></param>
public record GetAsoExperimentOverviewQueryHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                   IStoreContentService ContentService) : IInterceptable, IQueryHandler<GetAsoExperimentOverviewQuery, AsoExperimentOverviewDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreContentService _contentService = ContentService;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentOverviewDto>> Handle(GetAsoExperimentOverviewQuery request, CancellationToken cancellationToken)
    {
        var records = await _experimentRepository.GetAllAsync(r => r.OrganizationId == request.OrganizationId && r.AppId == request.AppId, cancellationToken: cancellationToken);
        var storeApps = await _contentService.GetStoreAppsAsync(request.OrganizationId, request.AppId, cancellationToken);

        return Response<AsoExperimentOverviewDto>.Success(new AsoExperimentOverviewDto
        {
            Experiments = [.. records.OrderByDescending(r => r.Id).Select(AsoExperimentViews.Summary)],
            Capabilities = [.. storeApps.OrderBy(s => s.Store).Select(s => AsoExperimentViews.Capability(s.StoreAppId, s.Store))],
        });
    }
}
