namespace Storeasy.Application.Interfaces;

/// <summary>
/// Puts store work on the database backed queue. The requester (current user) and channel (<see cref="IAuditTrail.Channel"/>)
/// are recorded, a <see cref="ActivityEventTypes.JobQueued"/> event is published, and the runner picks the jobs up.
/// </summary>
public interface IStoreJobQueue
{
    /// <summary>
    /// Queues <paramref name="requests"/> in one write and returns their ids in the same order.
    /// </summary>
    /// <param name="requests"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<long>> EnqueueAsync(IReadOnlyList<StoreJobRequest> requests, CancellationToken cancellationToken);
}

/// <summary>
/// A job to queue.
/// </summary>
/// <param name="OrganizationId">Owning organization.</param>
/// <param name="Kind">What to do.</param>
/// <param name="Payload">Parameters, serialized to JSON. Must not carry a secret.</param>
/// <param name="Store">Store the job talks to, if one.</param>
/// <param name="AppId">storeasy app the job works on, if one.</param>
/// <param name="MaxAttempts">Attempts allowed; null for the configured default.</param>
public sealed record StoreJobRequest(int OrganizationId, StoreJobKind Kind, object Payload, StoreKind? Store = null, int? AppId = null, int? MaxAttempts = null);
