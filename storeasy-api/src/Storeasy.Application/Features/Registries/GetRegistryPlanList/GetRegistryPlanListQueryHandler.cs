using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetRegistryPlanList;

/// <summary>
/// Handles <see cref="GetRegistryPlanListQuery"/>.
/// </summary>
/// <param name="planRepository"></param>
public class GetRegistryPlanListQueryHandler(IStoreasyRepositoryBase<RegistryPlan> planRepository) : IInterceptable, IQueryHandler<GetRegistryPlanListQuery, List<RegistryPlanDto>>
{
    private readonly IStoreasyRepositoryBase<RegistryPlan> _planRepository = planRepository;

    /// <inheritdoc/>
    public async Task<Response<List<RegistryPlanDto>>> Handle(GetRegistryPlanListQuery request, CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetAllAsync(p => p.OrganizationId == request.OrganizationId && (request.PackageGroupId == null || p.PackageGroupId == request.PackageGroupId),
                                                      projection: RegistryPlanDto.Projection,
                                                      cancellationToken: cancellationToken);

        // A group release covers its members too: a package's plans include them.
        return Response<List<RegistryPlanDto>>.Success([.. plans.Where(p => request.TrackedPackageId == null || p.TrackedPackageId == request.TrackedPackageId || p.PackageIds.Contains(request.TrackedPackageId.Value))
                                                                .OrderByDescending(p => p.Id)
                                                                .Take(GetRegistryPlanListQuery.MaxPlans)]);
    }
}
