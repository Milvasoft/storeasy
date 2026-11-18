using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.TestFlight.SyncTestFlight;

/// <summary>
/// Handles <see cref="SyncTestFlightCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
public record SyncTestFlightCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncTestFlightCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncTestFlightCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncTestFlight, new SyncTestFlightPayload(storeApp.Id), StoreKind.Apple, storeApp.AppId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
