using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.GetBetaTesterList;

/// <summary>
/// Handles <see cref="GetBetaTesterListQuery"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="GroupRepository"></param>
/// <param name="TesterRepository"></param>
public record GetBetaTesterListQueryHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                            IStoreasyRepositoryBase<BetaGroup> GroupRepository,
                                            IStoreasyRepositoryBase<BetaTester> TesterRepository) : IInterceptable, IListQueryHandler<GetBetaTesterListQuery, BetaTesterDto>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly IStoreasyRepositoryBase<BetaTester> _testerRepository = TesterRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<BetaTesterDto>> Handle(GetBetaTesterListQuery request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return ListResponse<BetaTesterDto>.Error(null, MessageKey.StoreAppNotFound);

        string groupKey = null;

        if (request.GroupId.HasValue)
        {
            var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.GroupId && g.StoreAppId == storeApp.Id,
                                                                      projection: g => new { g.ExternalId },
                                                                      cancellationToken: cancellationToken);

            if (group == null)
                return ListResponse<BetaTesterDto>.Error(null, MessageKey.BetaGroupNotFound);

            groupKey = $",{group.ExternalId},";
        }

        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(BetaTesterDto.Email), Type = Milvasoft.Components.Rest.Enums.SortType.Asc };

        var response = await _testerRepository.GetAllAsync(request,
                                                           condition: t => t.StoreAppId == storeApp.Id && (groupKey == null || t.GroupKeys.Contains(groupKey)),
                                                           projection: BetaTesterDto.Projection,
                                                           cancellationToken: cancellationToken);

        response.Data?.ForEach(t => t.Complete());

        return response;
    }
}
