using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Registries;

/// <summary>
/// <see cref="StoreJobKind.RegistryPlanApply"/>: writes an approved registry plan (<see cref="IRegistryPlanService.ApplyAsync"/>):
/// the version bump commit, the tag or the workflow dispatch of a release, or a registry operation with the stored key.
/// </summary>
/// <param name="planService"></param>
public sealed class RegistryPlanApplyJobHandler(IRegistryPlanService planService) : IStoreJobHandler
{
    private readonly IRegistryPlanService _planService = planService;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.RegistryPlanApply;

    /// <inheritdoc/>
    public Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<RegistryPlanApplyPayload>() ?? throw new StoreJobException("The job names no plan.", transient: false);

        return _planService.ApplyAsync(payload.PlanId, context, cancellationToken);
    }
}
