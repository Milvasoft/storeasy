using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Domain.Analytics;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.StoreMetrics.EnableAppleAnalytics;

/// <summary>
/// Handles <see cref="EnableAppleAnalyticsCommand"/>: checks the confirmation and the record, writes the audit entry and
/// queues the <see cref="StoreJobKind.EnableAppleAnalytics"/> job (which reuses a request that already exists at Apple).
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="FeedRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
public record EnableAppleAnalyticsCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                 IStoreasyRepositoryBase<StoreMetricFeed> FeedRepository,
                                                 IStoreJobQueue StoreJobQueue,
                                                 IAuditTrail AuditTrail,
                                                 ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<EnableAppleAnalyticsCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<StoreMetricFeed> _feedRepository = FeedRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(EnableAppleAnalyticsCommand request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && !s.App.IsDeleted,
                                                                     projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, Store = s.Store, ExternalId = s.ExternalId, StoreConnectionId = s.StoreConnectionId },
                                                                     cancellationToken: cancellationToken);

        if (record == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (record.Store != StoreKind.Apple)
            return Response<long>.Error(0, MessageKey.AppleAnalyticsAppleOnly);

        if (record.StoreConnectionId == null)
            return Response<long>.Error(0, MessageKey.StoreMetricsNoConnection);

        if (!request.Confirm)
            return Response<long>.Error(0, MessageKey.AppleAnalyticsConfirmRequired);

        var feed = await _feedRepository.GetFirstOrDefaultAsync(f => f.StoreAppId == record.Id,
                                                               projection: f => new StoreMetricFeed { Id = f.Id, ExternalRequestId = f.ExternalRequestId },
                                                               cancellationToken: cancellationToken);

        if (!string.IsNullOrEmpty(feed?.ExternalRequestId))
            return Response<long>.Error(0, MessageKey.AppleAnalyticsAlreadyEnabled);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId,
                                                                            StoreJobKind.EnableAppleAnalytics,
                                                                            new EnableAppleAnalyticsPayload(record.Id, _currentUser.UserName),
                                                                            StoreKind.Apple,
                                                                            record.AppId)],
                                                       cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AppleAnalyticsEnabled,
                                     record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                     $"App Store analytics reports requested for app {record.ExternalId}.",
                                     StoreKind.Apple,
                                     new { record.AppId, StoreAppId = record.Id, AppleAppId = record.ExternalId, JobId = jobIds[0] },
                                     cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
