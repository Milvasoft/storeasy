using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Apps.GetAppList;

/// <summary>
/// Handles <see cref="GetAppListQuery"/>. Sorted by name by default.
/// </summary>
/// <param name="appRepository"></param>
public class GetAppListQueryHandler(IStoreasyRepositoryBase<App> appRepository) : IInterceptable, IListQueryHandler<GetAppListQuery, AppListDto>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = appRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<AppListDto>> Handle(GetAppListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(50);
        request.Sorting ??= new SortRequest { SortBy = nameof(AppListDto.Name), Type = Milvasoft.Components.Rest.Enums.SortType.Asc };

        var response = await _appRepository.GetAllAsync(request,
                                                        condition: a => a.OrganizationId == request.OrganizationId,
                                                        projection: AppListDto.Projection,
                                                        cancellationToken: cancellationToken);

        return response;
    }
}
