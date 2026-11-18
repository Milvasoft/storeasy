using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.Subscriptions.ApplySubscriptionPlan;

/// <summary>
/// Handles <see cref="ApplySubscriptionPlanCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
[Log]
public record ApplySubscriptionPlanCommandHandler(IMonetizationService MonetizationService) : IInterceptable, ICommandHandler<ApplySubscriptionPlanCommand, ChangePlanDto>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(ApplySubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        return _monetizationService.ApproveAsync(request.OrganizationId, request.PlanId, request.Confirm, [ChangePlanKind.Monetization], cancellationToken);
    }
}
