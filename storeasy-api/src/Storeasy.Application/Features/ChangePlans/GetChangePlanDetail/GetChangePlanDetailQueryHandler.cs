using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;

/// <summary>
/// Handles <see cref="GetChangePlanDetailQuery"/>.
/// </summary>
/// <param name="changePlanService"></param>
public class GetChangePlanDetailQueryHandler(IChangePlanService changePlanService) : IInterceptable, IQueryHandler<GetChangePlanDetailQuery, ChangePlanDto>
{
    private readonly IChangePlanService _changePlanService = changePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(GetChangePlanDetailQuery request, CancellationToken cancellationToken)
    {
        var plan = await _changePlanService.GetAsync(request.OrganizationId, request.PlanId, cancellationToken);

        return plan == null ? Response<ChangePlanDto>.Error(null, MessageKey.ChangePlanNotFound) : Response<ChangePlanDto>.Success(plan);
    }
}
