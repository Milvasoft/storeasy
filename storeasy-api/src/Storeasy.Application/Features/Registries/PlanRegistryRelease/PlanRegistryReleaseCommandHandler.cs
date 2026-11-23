using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.PlanRegistryRelease;

/// <summary>
/// Handles <see cref="PlanRegistryReleaseCommand"/>.
/// </summary>
/// <param name="PlanService"></param>
public record PlanRegistryReleaseCommandHandler(IRegistryPlanService PlanService) : IInterceptable, ICommandHandler<PlanRegistryReleaseCommand, RegistryPlanDto>
{
    private readonly IRegistryPlanService _planService = PlanService;

    /// <inheritdoc/>
    public Task<Response<RegistryPlanDto>> Handle(PlanRegistryReleaseCommand request, CancellationToken cancellationToken)
        => _planService.PlanReleaseAsync(request.OrganizationId, request.TrackedPackageId, request.PackageGroupId, request.Version.Trim(), cancellationToken);
}
