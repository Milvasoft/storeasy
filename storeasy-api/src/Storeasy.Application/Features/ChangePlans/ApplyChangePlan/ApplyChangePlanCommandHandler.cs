using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.ApplyChangePlan;

/// <summary>
/// Handles <see cref="ApplyChangePlanCommand"/>.
/// </summary>
/// <param name="ChangePlanService"></param>
[Log]
public record ApplyChangePlanCommandHandler(IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<ApplyChangePlanCommand, ChangePlanDto>
{
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(ApplyChangePlanCommand request, CancellationToken cancellationToken)
    {
        return _changePlanService.ApproveAsync(request.OrganizationId, request.PlanId, request.Confirm, cancellationToken);
    }
}
