using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Features.AsoExperiments.GetAsoExperimentDetail;

/// <summary>
/// Handles <see cref="GetAsoExperimentDetailQuery"/>.
/// </summary>
/// <param name="ExperimentRepository"></param>
/// <param name="PlanRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
public record GetAsoExperimentDetailQueryHandler(IStoreasyRepositoryBase<AsoExperiment> ExperimentRepository,
                                                 IStoreasyRepositoryBase<ChangePlan> PlanRepository,
                                                 IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                 IMilvaLocalizer Localizer,
                                                 TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetAsoExperimentDetailQuery, AsoExperimentDto>
{
    private readonly IStoreasyRepositoryBase<AsoExperiment> _experimentRepository = ExperimentRepository;
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = PlanRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<AsoExperimentDto>> Handle(GetAsoExperimentDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await _experimentRepository.DetailAsync(_planRepository, _storeAppRepository, request.OrganizationId, request.Id, _timeProvider.GetUtcNow().UtcDateTime, _localizer, cancellationToken);

        return detail == null ? Response<AsoExperimentDto>.Error(null, MessageKey.AsoExperimentNotFound) : Response<AsoExperimentDto>.Success(detail);
    }
}
