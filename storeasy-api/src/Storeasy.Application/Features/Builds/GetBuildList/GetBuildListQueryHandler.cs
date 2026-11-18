using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Builds.GetBuildList;

/// <summary>
/// Handles <see cref="GetBuildListQuery"/>.
/// </summary>
/// <param name="buildRepository"></param>
public class GetBuildListQueryHandler(IStoreasyRepositoryBase<Build> buildRepository) : IInterceptable, IListQueryHandler<GetBuildListQuery, BuildDto>
{
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;

    /// <inheritdoc/>
    public Task<ListResponse<BuildDto>> Handle(GetBuildListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(BuildDto.UploadedDate), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        return _buildRepository.GetAllAsync(request,
                                                  condition: b => b.StoreAppId == request.StoreAppId && b.OrganizationId == request.OrganizationId,
                                                  projection: BuildDto.Projection,
                                                  cancellationToken: cancellationToken);
    }
}
