using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.RejectChangePlan;

/// <summary>
/// Handles <see cref="RejectChangePlanCommand"/>.
/// </summary>
/// <param name="ChangePlanService"></param>
[Log]
public record RejectChangePlanCommandHandler(IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<RejectChangePlanCommand, ChangePlanDto>
{
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(RejectChangePlanCommand request, CancellationToken cancellationToken)
    {
        return _changePlanService.DecideAsync(request.OrganizationId, request.PlanId, ChangePlanStatus.Rejected, request.Reason?.Trim(), cancellationToken);
    }
}
