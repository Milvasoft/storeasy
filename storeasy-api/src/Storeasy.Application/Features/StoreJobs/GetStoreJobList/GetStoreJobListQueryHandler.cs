using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreJobs.GetStoreJobList;

/// <summary>
/// Handles <see cref="GetStoreJobListQuery"/>.
/// </summary>
/// <param name="jobRepository"></param>
public class GetStoreJobListQueryHandler(IStoreasyRepositoryBase<StoreJob> jobRepository) : IInterceptable, IListQueryHandler<GetStoreJobListQuery, StoreJobListDto>
{
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<StoreJobListDto>> Handle(GetStoreJobListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(StoreJobListDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _jobRepository.GetAllAsync(request,
                                                        condition: j => j.OrganizationId == request.OrganizationId && (request.AppId == null || j.AppId == request.AppId),
                                                        projection: StoreJobListDto.Projection,
                                                        cancellationToken: cancellationToken);

        return response;
    }
}
