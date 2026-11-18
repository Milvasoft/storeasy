using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.DiscardChangePlan;

/// <summary>
/// Handles <see cref="DiscardChangePlanCommand"/>.
/// </summary>
/// <param name="ChangePlanService"></param>
[Log]
public record DiscardChangePlanCommandHandler(IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<DiscardChangePlanCommand, ChangePlanDto>
{
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(DiscardChangePlanCommand request, CancellationToken cancellationToken)
    {
        return _changePlanService.DecideAsync(request.OrganizationId, request.PlanId, ChangePlanStatus.Discarded, null, cancellationToken);
    }
}
