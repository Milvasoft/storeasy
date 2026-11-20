using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Builds.GetBuildList;

/// <summary>
/// Handles <see cref="GetBuildListQuery"/>. Each build carries the upload session whose package storeasy still keeps
/// (<see cref="BuildPackages"/>).
/// </summary>
/// <param name="buildRepository"></param>
/// <param name="sessionRepository"></param>
public class GetBuildListQueryHandler(IStoreasyRepositoryBase<Build> buildRepository,
                                      IStoreasyRepositoryBase<ArtifactSession> sessionRepository) : IInterceptable, IListQueryHandler<GetBuildListQuery, BuildDto>
{
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = sessionRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<BuildDto>> Handle(GetBuildListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(BuildDto.UploadedDate), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        var response = await _buildRepository.GetAllAsync(request,
                                                          condition: b => b.StoreAppId == request.StoreAppId && b.OrganizationId == request.OrganizationId,
                                                          projection: BuildDto.Projection,
                                                          cancellationToken: cancellationToken);

        if (response?.Data != null)
            await BuildPackages.FillAsync(_sessionRepository, request.OrganizationId, [.. response.Data], cancellationToken);

        return response;
    }
}
