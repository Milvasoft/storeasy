using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.GetBetaGroupList;

/// <summary>
/// Handles <see cref="GetBetaGroupListQuery"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="GroupRepository"></param>
/// <param name="BuildRepository"></param>
public record GetBetaGroupListQueryHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                           IStoreasyRepositoryBase<BetaGroup> GroupRepository,
                                           IStoreasyRepositoryBase<Build> BuildRepository) : IInterceptable, IQueryHandler<GetBetaGroupListQuery, List<BetaGroupDto>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;

    /// <inheritdoc/>
    public async Task<Response<List<BetaGroupDto>>> Handle(GetBetaGroupListQuery request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<List<BetaGroupDto>>.Error(null, MessageKey.StoreAppNotFound);

        var groups = await _groupRepository.GetAllAsync(g => g.StoreAppId == storeApp.Id,
                                                        projection: g => new BetaGroup
                                                        {
                                                            Id = g.Id,
                                                            Name = g.Name,
                                                            IsInternal = g.IsInternal,
                                                            TesterCount = g.TesterCount,
                                                            PublicLinkEnabled = g.PublicLinkEnabled,
                                                            PublicLink = g.PublicLink,
                                                            PublicLinkLimit = g.PublicLinkLimit,
                                                            PublicLinkLimitEnabled = g.PublicLinkLimitEnabled,
                                                            BuildExternalIds = g.BuildExternalIds,
                                                            SyncedDate = g.SyncedDate,
                                                        },
                                                        cancellationToken: cancellationToken);

        var buildIds = (await _buildRepository.GetAllAsync(b => b.StoreAppId == storeApp.Id,
                                                           projection: b => new Build { Id = b.Id, ExternalId = b.ExternalId },
                                                           cancellationToken: cancellationToken)).ToDictionary(b => b.ExternalId, b => b.Id);

        var result = groups.OrderByDescending(g => g.IsInternal).ThenBy(g => g.Name).Select(g => new BetaGroupDto
        {
            Id = g.Id,
            Name = g.Name,
            IsInternal = g.IsInternal,
            TesterCount = g.TesterCount,
            PublicLinkEnabled = g.PublicLinkEnabled,
            PublicLink = g.PublicLink,
            PublicLinkLimit = g.PublicLinkLimit,
            PublicLinkLimitEnabled = g.PublicLinkLimitEnabled,
            BuildIds = [.. (g.BuildExternalIds ?? []).Where(buildIds.ContainsKey).Select(e => buildIds[e])],
            SyncedDate = g.SyncedDate,
        }).ToList();

        return Response<List<BetaGroupDto>>.Success(result);
    }
}
