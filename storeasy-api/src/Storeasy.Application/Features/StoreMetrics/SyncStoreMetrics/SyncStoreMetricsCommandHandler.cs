using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.StoreMetrics.SyncStoreMetrics;

/// <summary>
/// Handles <see cref="SyncStoreMetricsCommand"/>. Without a record to read it answers 404.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
public record SyncStoreMetricsCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncStoreMetricsCommand, List<long>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<List<long>>> Handle(SyncStoreMetricsCommand request, CancellationToken cancellationToken)
    {
        var records = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId
                                                                  && (request.AppId == null || s.AppId == request.AppId)
                                                                  && s.StoreConnectionId != null
                                                                  && !s.App.IsDeleted,
                                                             projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, Store = s.Store },
                                                             cancellationToken: cancellationToken);

        if (records.Count == 0)
            return Response<List<long>>.Error(null, MessageKey.StoreAppNotFound);

        var jobIds = await _storeJobQueue.EnqueueAsync([.. records.Select(r => new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncStoreMetrics, new SyncStoreMetricsPayload(r.Id), r.Store, r.AppId))], cancellationToken);

        return Response<List<long>>.Success(jobIds);
    }
}
