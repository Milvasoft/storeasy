using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Iap.ApplyIapPlan;

/// <summary>
/// Handles <see cref="ApplyIapPlanCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
[Log]
public record ApplyIapPlanCommandHandler(IMonetizationService MonetizationService) : IInterceptable, ICommandHandler<ApplyIapPlanCommand, ChangePlanDto>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(ApplyIapPlanCommand request, CancellationToken cancellationToken)
    {
        return _monetizationService.ApproveAsync(request.OrganizationId, request.PlanId, request.Confirm, [ChangePlanKind.Monetization], cancellationToken);
    }
}
