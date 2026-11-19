using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;
using System.Linq.Expressions;

namespace Storeasy.Application.Features.Apps.GetAppList;

/// <summary>
/// Handles <see cref="GetAppListQuery"/>. Without a sort in the request the list follows the order members chose
/// (<see cref="App.DisplayOrder"/>), and the apps never placed follow by name; a sort in the request replaces that order.
/// The store and hidden filters are part of the query.
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

        Expression<Func<App, bool>> condition = a => a.OrganizationId == request.OrganizationId
                                                     && (request.Store == null || a.StoreApps.Any(s => s.Store == request.Store))
                                                     && (request.IncludeHidden || (request.Hidden ? a.HiddenDate != null : a.HiddenDate == null));

        if (!string.IsNullOrWhiteSpace(request.Sorting?.SortBy))
            return await _appRepository.GetAllAsync(request, condition: condition, projection: AppListDto.Projection, cancellationToken: cancellationToken);

        // The chosen order needs two keys (the place, then the name), so the matching apps are ordered from their keys
        // (one light read with the filters), and only the apps of the asked page are read in full.
        var keys = await _appRepository.GetAllAsync(new ListRequest { Filtering = request.Filtering },
                                                    condition: condition,
                                                    projection: a => new AppListDto { Id = a.Id, Name = a.Name, DisplayOrder = a.DisplayOrder },
                                                    cancellationToken: cancellationToken);

        if (!keys.IsSuccess)
            return keys;

        var ordered = (keys.Data ?? []).OrderBy(a => a.DisplayOrder == null)
                                       .ThenBy(a => a.DisplayOrder)
                                       .ThenBy(a => a.Name, StringComparer.InvariantCultureIgnoreCase)
                                       .ThenBy(a => a.Id)
                                       .ToList();

        var page = ordered.AsQueryable().ToListResponse(new ListRequest { PageNumber = request.PageNumber, RowCount = request.RowCount });
        var pageIds = page.Data?.Select(a => a.Id).ToList() ?? [];

        if (pageIds.Count == 0)
            return page;

        var apps = await _appRepository.GetAllAsync(a => pageIds.Contains(a.Id), projection: AppListDto.Projection, cancellationToken: cancellationToken);
        var byId = apps.ToDictionary(a => a.Id);

        page.Data = [.. pageIds.Where(byId.ContainsKey).Select(id => byId[id])];

        return page;
    }
}
