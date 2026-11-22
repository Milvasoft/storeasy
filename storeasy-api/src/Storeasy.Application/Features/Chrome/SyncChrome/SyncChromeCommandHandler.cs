using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Chrome.SyncChrome;

/// <summary>
/// Handles <see cref="SyncChromeCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
[Log]
public record SyncChromeCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncChromeCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncChromeCommand request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Chrome && !s.App.IsDeleted,
                                                                      projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, StoreConnectionId = s.StoreConnectionId },
                                                                      cancellationToken: cancellationToken);

        if (record == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (record.StoreConnectionId == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotConnected);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.ChromeSync, new ChromeSyncPayload(record.StoreConnectionId.Value, record.Id), StoreKind.Chrome, record.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
