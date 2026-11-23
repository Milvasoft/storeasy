using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.PlanRegistryOperation;

/// <summary>
/// Handles <see cref="PlanRegistryOperationCommand"/>.
/// </summary>
/// <param name="PlanService"></param>
public record PlanRegistryOperationCommandHandler(IRegistryPlanService PlanService) : IInterceptable, ICommandHandler<PlanRegistryOperationCommand, RegistryPlanDto>
{
    private readonly IRegistryPlanService _planService = PlanService;

    /// <inheritdoc/>
    public Task<Response<RegistryPlanDto>> Handle(PlanRegistryOperationCommand request, CancellationToken cancellationToken)
        => _planService.PlanOperationAsync(request.OrganizationId, request.TrackedPackageId, request.Kind, request.Version.Trim(), request.Argument?.Trim(), cancellationToken);
}
