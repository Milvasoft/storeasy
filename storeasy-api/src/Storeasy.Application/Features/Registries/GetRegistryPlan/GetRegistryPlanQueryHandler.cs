using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetRegistryPlan;

/// <summary>
/// Handles <see cref="GetRegistryPlanQuery"/>. A plan of another organization is reported as not found.
/// </summary>
/// <param name="planRepository"></param>
public class GetRegistryPlanQueryHandler(IStoreasyRepositoryBase<RegistryPlan> planRepository) : IInterceptable, IQueryHandler<GetRegistryPlanQuery, RegistryPlanDto>
{
    private readonly IStoreasyRepositoryBase<RegistryPlan> _planRepository = planRepository;

    /// <inheritdoc/>
    public async Task<Response<RegistryPlanDto>> Handle(GetRegistryPlanQuery request, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetFirstOrDefaultAsync(p => p.Id == request.PlanId && p.OrganizationId == request.OrganizationId, projection: RegistryPlanDto.Projection, cancellationToken: cancellationToken);

        return plan == null ? Response<RegistryPlanDto>.Error(null, MessageKey.RegistryPlanNotFound) : Response<RegistryPlanDto>.Success(plan);
    }
}
