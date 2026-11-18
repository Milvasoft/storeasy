using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Lookups and job queueing shared by the TestFlight features.
/// </summary>
public static class TestFlightExtensions
{
    /// <summary>
    /// The organization's App Store record <paramref name="storeAppId"/> (TestFlight exists only there); null when it is not one.
    /// </summary>
    /// <param name="storeAppRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="storeAppId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<StoreApp> FindAppleRecordAsync(this IStoreasyRepositoryBase<StoreApp> storeAppRepository, int organizationId, int storeAppId, CancellationToken cancellationToken)
        => storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == storeAppId && s.OrganizationId == organizationId && s.Store == StoreKind.Apple && !s.App.IsDeleted,
                                                     projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId },
                                                     cancellationToken: cancellationToken);

    /// <summary>
    /// The organization's App Store build <paramref name="buildId"/> with what the TestFlight features check; null when
    /// it is not one.
    /// </summary>
    /// <param name="buildRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="buildId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<Build> FindAppleBuildAsync(this IStoreasyRepositoryBase<Build> buildRepository, int organizationId, long buildId, CancellationToken cancellationToken)
        => buildRepository.GetFirstOrDefaultAsync(b => b.Id == buildId && b.OrganizationId == organizationId && b.Store == StoreKind.Apple,
                                                  projection: b => new Build { Id = b.Id, StoreAppId = b.StoreAppId, ExternalId = b.ExternalId, Version = b.Version, BuildNumber = b.BuildNumber, ProcessingState = b.ProcessingState, ExpiryDate = b.ExpiryDate },
                                                  cancellationToken: cancellationToken);

    /// <summary>
    /// Whether a build can no longer be tested (TestFlight builds expire after 90 days).
    /// </summary>
    public static bool IsExpired(this Build build, DateTime now) => build.ProcessingState == BuildProcessingState.Expired || (build.ExpiryDate.HasValue && build.ExpiryDate.Value <= now);

    /// <summary>
    /// Queues one TestFlight write and returns the job id.
    /// </summary>
    /// <param name="storeJobQueue"></param>
    /// <param name="organizationId"></param>
    /// <param name="appId"></param>
    /// <param name="payload"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<long> EnqueueTestFlightAsync(this IStoreJobQueue storeJobQueue, int organizationId, int? appId, UpdateTestFlightPayload payload, CancellationToken cancellationToken)
    {
        var jobIds = await storeJobQueue.EnqueueAsync([new StoreJobRequest(organizationId, StoreJobKind.UpdateTestFlight, payload, StoreKind.Apple, appId)], cancellationToken);

        return jobIds[0];
    }
}
