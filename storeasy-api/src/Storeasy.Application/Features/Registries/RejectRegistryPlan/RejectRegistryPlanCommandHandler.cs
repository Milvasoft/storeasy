using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.RejectRegistryPlan;

/// <summary>
/// Handles <see cref="RejectRegistryPlanCommand"/>.
/// </summary>
/// <param name="PlanService"></param>
public record RejectRegistryPlanCommandHandler(IRegistryPlanService PlanService) : IInterceptable, ICommandHandler<RejectRegistryPlanCommand, RegistryPlanDto>
{
    private readonly IRegistryPlanService _planService = PlanService;

    /// <inheritdoc/>
    public Task<Response<RegistryPlanDto>> Handle(RejectRegistryPlanCommand request, CancellationToken cancellationToken)
        => _planService.RejectAsync(request.OrganizationId, request.PlanId, cancellationToken);
}
