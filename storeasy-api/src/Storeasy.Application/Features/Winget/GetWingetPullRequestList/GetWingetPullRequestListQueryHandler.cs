using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.Winget;

namespace Storeasy.Application.Features.Winget.GetWingetPullRequestList;

/// <summary>
/// Handles <see cref="GetWingetPullRequestListQuery"/>. A record of another organization or another store is reported as
/// not found.
/// </summary>
/// <param name="packageRepository"></param>
/// <param name="pullRequestRepository"></param>
public class GetWingetPullRequestListQueryHandler(IStoreasyRepositoryBase<WingetPackage> packageRepository,
                                                  IStoreasyRepositoryBase<WingetPullRequest> pullRequestRepository) : IInterceptable, IQueryHandler<GetWingetPullRequestListQuery, List<WingetPullRequestDto>>
{
    private readonly IStoreasyRepositoryBase<WingetPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = pullRequestRepository;

    /// <inheritdoc/>
    public async Task<Response<List<WingetPullRequestDto>>> Handle(GetWingetPullRequestListQuery request, CancellationToken cancellationToken)
    {
        var exists = await _packageRepository.GetFirstOrDefaultAsync(p => p.StoreAppId == request.StoreAppId && p.OrganizationId == request.OrganizationId && !p.StoreApp.App.IsDeleted,
                                                                     projection: p => (int?)p.Id,
                                                                     cancellationToken: cancellationToken);

        if (!exists.HasValue)
            return Response<List<WingetPullRequestDto>>.Error(null, MessageKey.StoreAppNotFound);

        var items = await _pullRequestRepository.GetAllAsync(p => p.StoreAppId == request.StoreAppId && p.OrganizationId == request.OrganizationId,
                                                             projection: WingetPullRequestDto.Projection,
                                                             cancellationToken: cancellationToken);

        return Response<List<WingetPullRequestDto>>.Success([.. items.OrderByDescending(p => p.OpenedDate ?? DateTime.MinValue).ThenByDescending(p => p.Number)]);
    }
}
