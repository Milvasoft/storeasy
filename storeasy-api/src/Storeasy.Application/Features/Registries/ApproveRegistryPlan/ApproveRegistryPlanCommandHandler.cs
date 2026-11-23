using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.ApproveRegistryPlan;

/// <summary>
/// Handles <see cref="ApproveRegistryPlanCommand"/>.
/// </summary>
/// <param name="PlanService"></param>
public record ApproveRegistryPlanCommandHandler(IRegistryPlanService PlanService) : IInterceptable, ICommandHandler<ApproveRegistryPlanCommand, RegistryPlanDto>
{
    private readonly IRegistryPlanService _planService = PlanService;

    /// <inheritdoc/>
    public Task<Response<RegistryPlanDto>> Handle(ApproveRegistryPlanCommand request, CancellationToken cancellationToken)
        => _planService.ApproveAsync(request.OrganizationId, request.PlanId, request.Confirm, cancellationToken);
}
