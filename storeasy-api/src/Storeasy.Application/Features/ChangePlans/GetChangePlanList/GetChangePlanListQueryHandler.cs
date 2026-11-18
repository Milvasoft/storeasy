using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Content;

namespace Storeasy.Application.Features.ChangePlans.GetChangePlanList;

/// <summary>
/// Handles <see cref="GetChangePlanListQuery"/>.
/// </summary>
/// <param name="planRepository"></param>
/// <param name="timeProvider"></param>
public class GetChangePlanListQueryHandler(IStoreasyRepositoryBase<ChangePlan> planRepository,
                                           TimeProvider timeProvider) : IInterceptable, IListQueryHandler<GetChangePlanListQuery, ChangePlanListDto>
{
    private readonly IStoreasyRepositoryBase<ChangePlan> _planRepository = planRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<ListResponse<ChangePlanListDto>> Handle(GetChangePlanListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(ChangePlanListDto.CreationDate), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _planRepository.GetAllAsync(request,
                                                         condition: p => p.OrganizationId == request.OrganizationId
                                                                         && (request.Status == null || p.Status == request.Status)
                                                                         && (request.AppId == null || p.AppId == request.AppId)
                                                                         && (request.Store == null || p.Store == request.Store),
                                                         projection: ChangePlanListDto.Projection,
                                                         cancellationToken: cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var plan in response.Data ?? [])
            plan.Status = ChangePlanRules.Effective(plan.Status, plan.ExpiryDate, null, now);

        return response;
    }
}
