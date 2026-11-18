using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.GetBetaBuildList;

/// <summary>
/// Handles <see cref="GetBetaBuildListQuery"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="BuildRepository"></param>
/// <param name="BetaBuildRepository"></param>
/// <param name="GroupRepository"></param>
/// <param name="TimeProvider"></param>
public record GetBetaBuildListQueryHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                           IStoreasyRepositoryBase<Build> BuildRepository,
                                           IStoreasyRepositoryBase<BetaBuild> BetaBuildRepository,
                                           IStoreasyRepositoryBase<BetaGroup> GroupRepository,
                                           TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetBetaBuildListQuery, List<BetaBuildDto>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;
    private readonly IStoreasyRepositoryBase<BetaBuild> _betaBuildRepository = BetaBuildRepository;
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<BetaBuildDto>>> Handle(GetBetaBuildListQuery request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<List<BetaBuildDto>>.Error(null, MessageKey.StoreAppNotFound);

        var builds = await _buildRepository.GetAllAsync(b => b.StoreAppId == storeApp.Id,
                                                        projection: b => new Build { Id = b.Id, ExternalId = b.ExternalId, Version = b.Version, BuildNumber = b.BuildNumber, ProcessingState = b.ProcessingState, UploadedDate = b.UploadedDate, ExpiryDate = b.ExpiryDate },
                                                        cancellationToken: cancellationToken);

        var betaBuilds = (await _betaBuildRepository.GetAllAsync(b => b.StoreAppId == storeApp.Id,
                                                                 projection: b => new BetaBuild { BuildId = b.BuildId, BetaReviewState = b.BetaReviewState, WhatToTest = b.WhatToTest },
                                                                 cancellationToken: cancellationToken)).ToDictionary(b => b.BuildId);

        var groups = await _groupRepository.GetAllAsync(g => g.StoreAppId == storeApp.Id,
                                                        projection: g => new BetaGroup { Id = g.Id, BuildExternalIds = g.BuildExternalIds },
                                                        cancellationToken: cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var result = builds.OrderByDescending(b => b.UploadedDate).Select(b =>
        {
            var beta = betaBuilds.GetValueOrDefault(b.Id);
            var expired = b.IsExpired(now);

            return new BetaBuildDto
            {
                BuildId = b.Id,
                VersionString = b.Version,
                BuildNumber = b.BuildNumber,
                ProcessingState = b.ProcessingState,
                UploadedDate = b.UploadedDate,
                ExpirationDate = b.ExpiryDate,
                Expired = expired,
                DaysLeft = b.ExpiryDate.HasValue ? Math.Max(0, (int)Math.Floor((b.ExpiryDate.Value - now).TotalDays)) : null,
                BetaReviewState = beta?.BetaReviewState ?? BetaReviewState.NotSubmitted,
                WhatToTest = beta?.WhatToTest ?? [],
                GroupIds = [.. groups.Where(g => g.BuildExternalIds?.Contains(b.ExternalId) == true).Select(g => g.Id)],
            };
        }).ToList();

        return Response<List<BetaBuildDto>>.Success(result);
    }
}
