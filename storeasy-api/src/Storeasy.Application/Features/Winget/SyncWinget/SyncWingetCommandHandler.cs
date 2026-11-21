using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Winget.SyncWinget;

/// <summary>
/// Handles <see cref="SyncWingetCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
[Log]
public record SyncWingetCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncWingetCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncWingetCommand request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Winget && !s.App.IsDeleted,
                                                                      projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, StoreConnectionId = s.StoreConnectionId },
                                                                      cancellationToken: cancellationToken);

        if (record == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (record.StoreConnectionId == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotConnected);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.WingetSync, new WingetSyncPayload(record.StoreConnectionId.Value, record.Id), StoreKind.Winget, record.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
