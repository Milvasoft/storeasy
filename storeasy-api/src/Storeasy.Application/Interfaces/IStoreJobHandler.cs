namespace Storeasy.Application.Interfaces;

/// <summary>
/// Executes the store jobs of one <see cref="Kind"/>. Registered scoped; the runner resolves the handlers of a fresh
/// scope per job and picks the one for the job's kind. A handler reports its work as steps through the context, throws
/// <see cref="StoreJobException"/> (or lets a store call exception through) to fail an attempt, and returns the outcome
/// on success. The runner decides retries, cancellation and the final state.
/// </summary>
public interface IStoreJobHandler
{
    /// <summary>
    /// The kind of job this handler executes.
    /// </summary>
    StoreJobKind Kind { get; }

    /// <summary>
    /// Runs one attempt of the job.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken">Signalled when the host stops.</param>
    /// <returns></returns>
    Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What a handler sees of its job and how it reports progress. Every step call renews the job's lease, publishes a live
/// event and, at most about once a second, checks whether a member cancelled the job (then it throws
/// <see cref="OperationCanceledException"/>).
/// </summary>
public interface IStoreJobContext
{
    /// <summary>
    /// Job id.
    /// </summary>
    long JobId { get; }

    /// <summary>
    /// Owning organization.
    /// </summary>
    int OrganizationId { get; }

    /// <summary>
    /// storeasy app the job works on, if one.
    /// </summary>
    int? AppId { get; }

    /// <summary>
    /// Store the job talks to, if one.
    /// </summary>
    StoreKind? Store { get; }

    /// <summary>
    /// Current attempt, from 1.
    /// </summary>
    int Attempt { get; }

    /// <summary>
    /// Reads the job's payload as <typeparamref name="T"/>; default when there is none.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T GetPayload<T>();

    /// <summary>
    /// Starts a step and returns its position.
    /// </summary>
    /// <param name="name">Machine name (<c>area.action</c>).</param>
    /// <param name="direction"></param>
    /// <param name="message">Short note, redacted before it is stored.</param>
    /// <param name="total">Units in total, if known.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> BeginStepAsync(string name, StepDirection direction, string message = null, long? total = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports progress of a running step.
    /// </summary>
    /// <param name="order"></param>
    /// <param name="progress"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ReportProgressAsync(int order, long progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a step.
    /// </summary>
    /// <param name="order"></param>
    /// <param name="status"></param>
    /// <param name="message">Short note, redacted before it is stored.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EndStepAsync(int order, StoreJobStepStatus status, string message = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the secret of one of the organization's store connections for this execution only. Returns null when the
    /// connection does not exist in the organization (or was removed).
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<OpenedStoreConnection> OpenConnectionAsync(int connectionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A store connection opened inside a job. Keep it in memory only for the store calls of the job.
/// </summary>
/// <param name="Id">Connection id.</param>
/// <param name="Store">Store of the connection.</param>
/// <param name="Name">Display name.</param>
/// <param name="SecretJson">The decrypted secret JSON.</param>
public sealed record OpenedStoreConnection(int Id, StoreKind Store, string Name, string SecretJson);

/// <summary>
/// Result of a successful job.
/// </summary>
/// <param name="Summary">One line, human readable outcome.</param>
/// <param name="Result">Structured result, serialized to JSON; may be null.</param>
public sealed record StoreJobOutcome(string Summary, object Result = null);

/// <summary>
/// Fails the current attempt of a store job with a message that is safe to store and show (no secrets). A transient
/// failure (network, store 5xx, rate limit) is retried with backoff while attempts remain; a permanent one fails the job.
/// </summary>
/// <param name="message"></param>
/// <param name="transient"></param>
/// <param name="innerException"></param>
public class StoreJobException(string message, bool transient, Exception innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Whether a later attempt may succeed.
    /// </summary>
    public bool Transient { get; } = transient;
}
