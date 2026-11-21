using Storeasy.Application.Dtos.EventDtos;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Stores;
using System.Text.Json;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Queues the periodic store reads of the events phase as system jobs: version states per App Store connection, reviews
/// and TestFlight per store record, and Microsoft Store syncs while a submission is pending. A target that already has a queued or running job of the same kind is skipped.
/// </summary>
/// <param name="connectionRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="msSubmissionRepository"></param>
/// <param name="jobRepository"></param>
/// <param name="storeJobQueue"></param>
/// <param name="auditTrail"></param>
public sealed class ScheduledStoreSyncs(IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                                        IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                        IStoreasyRepositoryBase<MsSubmission> msSubmissionRepository,
                                        IStoreasyRepositoryBase<StoreJob> jobRepository,
                                        IStoreJobQueue storeJobQueue,
                                        IAuditTrail auditTrail)
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _msSubmissionRepository = msSubmissionRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly IStoreJobQueue _storeJobQueue = storeJobQueue;
    private readonly IAuditTrail _auditTrail = auditTrail;

    /// <summary>
    /// Queues a version state sync for every verified App Store Connect connection.
    /// </summary>
    public async Task<int> QueueVersionStateSyncsAsync(CancellationToken cancellationToken)
    {
        var connections = await _connectionRepository.GetAllAsync(c => c.Store == StoreKind.Apple && c.Status == ConnectionStatus.Verified,
                                                                  projection: c => new StoreConnection { Id = c.Id, OrganizationId = c.OrganizationId },
                                                                  cancellationToken: cancellationToken);

        return await QueueAsync(StoreJobKind.SyncVersionStates, [.. connections.Select(c => new StoreJobRequest(c.OrganizationId, StoreJobKind.SyncVersionStates, new SyncVersionStatesPayload(c.Id), StoreKind.Apple))], cancellationToken);
    }

    /// <summary>
    /// Queues a Microsoft Store sync for every verified Partner Center connection with a pending submission. Partner Center
    /// sends no notifications, so certification, publishing and a manual "Publish now" are only seen by reading again.
    /// </summary>
    public async Task<int> QueueMsSyncsAsync(CancellationToken cancellationToken)
    {
        var connectionIds = (await _msSubmissionRepository.GetAllAsync(s => s.IsPending
                                                                           && s.StoreApp.StoreConnectionId != null
                                                                           && !s.StoreApp.App.IsDeleted
                                                                           && s.StoreApp.StoreConnection.Status == ConnectionStatus.Verified,
                                                                      projection: s => new MsSubmission { OrganizationId = s.OrganizationId, StoreApp = new StoreApp { StoreConnectionId = s.StoreApp.StoreConnectionId } },
                                                                      cancellationToken: cancellationToken)).Select(s => (s.OrganizationId, ConnectionId: s.StoreApp.StoreConnectionId.Value))
                                                                                                            .Distinct()
                                                                                                            .ToList();

        return await QueueAsync(StoreJobKind.MsSync, [.. connectionIds.Select(c => new StoreJobRequest(c.OrganizationId, StoreJobKind.MsSync, new MsSyncPayload(c.ConnectionId), StoreKind.Microsoft))], cancellationToken);
    }

    /// <summary>
    /// Queues a review sync for every App Store and Google Play record with a connection.
    /// </summary>
    public async Task<int> QueueReviewSyncsAsync(CancellationToken cancellationToken)
    {
        var records = await RecordsAsync(false, cancellationToken);

        return await QueueAsync(StoreJobKind.SyncReviews, [.. records.Select(r => new StoreJobRequest(r.OrganizationId, StoreJobKind.SyncReviews, new SyncReviewsPayload(r.Id), r.Store, r.AppId))], cancellationToken);
    }

    /// <summary>
    /// Queues a TestFlight sync for every App Store record with a connection.
    /// </summary>
    public async Task<int> QueueTestFlightSyncsAsync(CancellationToken cancellationToken)
    {
        var records = await RecordsAsync(true, cancellationToken);

        return await QueueAsync(StoreJobKind.SyncTestFlight, [.. records.Select(r => new StoreJobRequest(r.OrganizationId, StoreJobKind.SyncTestFlight, new SyncTestFlightPayload(r.Id), StoreKind.Apple, r.AppId))], cancellationToken);
    }

    /// <summary>
    /// Queues a store page metrics sync for every store record (all three stores) with a verified connection.
    /// </summary>
    public async Task<int> QueueStoreMetricSyncsAsync(CancellationToken cancellationToken)
    {
        var records = await _storeAppRepository.GetAllAsync(s => s.StoreConnectionId != null
                                                                  && !s.App.IsDeleted
                                                                  && s.StoreConnection.Status == ConnectionStatus.Verified,
                                                             projection: s => new StoreApp { Id = s.Id, OrganizationId = s.OrganizationId, AppId = s.AppId, Store = s.Store },
                                                             cancellationToken: cancellationToken);

        return await QueueAsync(StoreJobKind.SyncStoreMetrics, [.. records.Select(r => new StoreJobRequest(r.OrganizationId, StoreJobKind.SyncStoreMetrics, new SyncStoreMetricsPayload(r.Id), r.Store, r.AppId))], cancellationToken);
    }

    private Task<List<StoreApp>> RecordsAsync(bool appleOnly, CancellationToken cancellationToken)
        => _storeAppRepository.GetAllAsync(s => (s.Store == StoreKind.Apple || (!appleOnly && s.Store == StoreKind.Google))
                                                && s.StoreConnectionId != null
                                                && !s.App.IsDeleted
                                                && s.StoreConnection.Status == ConnectionStatus.Verified,
                                           projection: s => new StoreApp { Id = s.Id, OrganizationId = s.OrganizationId, AppId = s.AppId, Store = s.Store },
                                           cancellationToken: cancellationToken);

    private async Task<int> QueueAsync(StoreJobKind kind, List<StoreJobRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
            return 0;

        var pending = (await _jobRepository.GetAllAsync(j => j.Kind == kind && (j.Status == StoreJobStatus.Queued || j.Status == StoreJobStatus.Running),
                                                        projection: j => new StoreJob { OrganizationId = j.OrganizationId, Payload = j.Payload },
                                                        cancellationToken: cancellationToken)).Select(j => (j.OrganizationId, j.Payload)).ToHashSet();

        var missing = requests.Where(r => !pending.Contains((r.OrganizationId, JsonSerializer.Serialize(r.Payload)))).ToList();

        if (missing.Count == 0)
            return 0;

        _auditTrail.Channel = ActionChannel.System;

        await _storeJobQueue.EnqueueAsync(missing, cancellationToken);

        return missing.Count;
    }
}
