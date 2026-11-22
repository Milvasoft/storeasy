using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Firefox.SyncFirefox;

/// <summary>
/// Handles <see cref="SyncFirefoxCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
[Log]
public record SyncFirefoxCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncFirefoxCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncFirefoxCommand request, CancellationToken cancellationToken)
    {
        var record = await _storeAppRepository.GetFirstOrDefaultAsync(s => s.Id == request.StoreAppId && s.OrganizationId == request.OrganizationId && s.Store == StoreKind.Firefox && !s.App.IsDeleted,
                                                                      projection: s => new StoreApp { Id = s.Id, AppId = s.AppId, StoreConnectionId = s.StoreConnectionId },
                                                                      cancellationToken: cancellationToken);

        if (record == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        if (record.StoreConnectionId == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotConnected);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.FirefoxSync, new FirefoxSyncPayload(record.StoreConnectionId.Value, record.Id), StoreKind.Firefox, record.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
