using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Apps.SyncApps;

/// <summary>
/// Handles <see cref="SyncAppsCommand"/>. One job per connection, queued in one write.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="Channels"></param>
[Log]
public record SyncAppsCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository, IStoreJobQueue StoreJobQueue, IEnumerable<IStoreChannel> Channels) : IInterceptable, ICommandHandler<SyncAppsCommand, List<long>>
{
    /// <summary>
    /// The first three stores, synced by their own jobs; registered channels (IStoreChannel) bring their own sync job.
    /// </summary>
    public static readonly StoreKind[] SupportedStores = [StoreKind.Apple, StoreKind.Google, StoreKind.Microsoft];

    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IReadOnlyList<IStoreChannel> _channels = [.. Channels];

    /// <inheritdoc/>
    public async Task<Response<List<long>>> Handle(SyncAppsCommand request, CancellationToken cancellationToken)
    {
        var channelStores = _channels.Select(c => c.Store).ToList();
        var connections = await _connectionRepository.GetAllAsync(c => c.OrganizationId == request.OrganizationId
                                                                        && (request.ConnectionId == null || c.Id == request.ConnectionId)
                                                                        && (SupportedStores.Contains(c.Store) || channelStores.Contains(c.Store)),
                                                                   projection: c => new { c.Id, c.Store },
                                                                   cancellationToken: cancellationToken);

        if (connections.Count == 0)
            return Response<List<long>>.Error(null, MessageKey.StoreConnectionNotFound);

        var jobIds = await _storeJobQueue.EnqueueAsync([.. connections.Select(c => _channels.LastOrDefault(channel => channel.Store == c.Store) is { } channel
                                                                     ? channel.SyncJob(request.OrganizationId, c.Id)
                                                                     : c.Store == StoreKind.Microsoft
                                                                     ? new StoreJobRequest(request.OrganizationId, StoreJobKind.MsSync, new MsSyncPayload(c.Id), c.Store)
                                                                     : new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncApps, new SyncAppsPayload(c.Id), c.Store))], cancellationToken);

        return Response<List<long>>.Success(jobIds);
    }
}
