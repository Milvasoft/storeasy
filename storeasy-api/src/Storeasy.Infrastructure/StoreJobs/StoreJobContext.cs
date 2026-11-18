using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// The <see cref="IStoreJobContext"/> of one attempt, created by <see cref="StoreJobExecutor"/>. Steps are rows of
/// <see cref="StoreJobStep"/>; every step call renews the lease and publishes a live event, and at most once per
/// <see cref="CancelCheckInterval"/> reads whether a member asked to stop the job.
/// </summary>
/// <param name="job">The claimed job (id, organization, app, store, kind, payload, attempt).</param>
/// <param name="firstStepOrder">Position of the first step of this attempt.</param>
/// <param name="services">Repositories and services of the executor's scope.</param>
public sealed class StoreJobContext(StoreJob job, int firstStepOrder, StoreJobContextServices services) : IStoreJobContext
{
    /// <summary>
    /// Shortest pause between two reads of the stop request.
    /// </summary>
    public static readonly TimeSpan CancelCheckInterval = TimeSpan.FromSeconds(1);

    private readonly StoreJob _job = job;
    private readonly StoreJobContextServices _services = services;
    private readonly Dictionary<int, StoreJobStep> _steps = [];

    private readonly int _firstStepOrder = firstStepOrder;

    private int _stepCount;
    private DateTime _lastCancelCheck = DateTime.MinValue;

    /// <inheritdoc/>
    public long JobId => _job.Id;

    /// <inheritdoc/>
    public int OrganizationId => _job.OrganizationId;

    /// <inheritdoc/>
    public int? AppId => _job.AppId;

    /// <inheritdoc/>
    public StoreKind? Store => _job.Store;

    /// <inheritdoc/>
    public int Attempt => _job.Attempt;

    /// <summary>
    /// Whether this attempt stopped because a member cancelled the job.
    /// </summary>
    public bool CancelObserved { get; private set; }

    /// <inheritdoc/>
    public T GetPayload<T>() => string.IsNullOrWhiteSpace(_job.Payload) ? default : JsonSerializer.Deserialize<T>(_job.Payload);

    /// <inheritdoc/>
    public async Task<int> BeginStepAsync(string name, StepDirection direction, string message = null, long? total = null, CancellationToken cancellationToken = default)
    {
        await RenewAndCheckCancelAsync(force: true, cancellationToken);

        var step = new StoreJobStep
        {
            StoreJobId = _job.Id,
            Order = _firstStepOrder + _stepCount++,
            Attempt = _job.Attempt,
            Name = Trim(name, StoreJobStep.NameMaxLength),
            Direction = direction,
            Status = StoreJobStepStatus.Running,
            Total = total,
            Message = SecretRedactor.RedactAndTrim(message, StoreJobStep.MessageMaxLength),
            StartDate = Now(),
        };

        await _services.StepRepository.AddAsync(step, cancellationToken);

        _steps[step.Order] = step;

        await PublishStepAsync(step, cancellationToken);

        return step.Order;
    }

    /// <inheritdoc/>
    public async Task ReportProgressAsync(int order, long progress, CancellationToken cancellationToken = default)
    {
        await RenewAndCheckCancelAsync(force: false, cancellationToken);

        if (!_steps.TryGetValue(order, out var step))
            return;

        step.Progress = progress;

        await _services.StepRepository.ExecuteUpdateAsync(step.Id, new SetPropertyBuilder<StoreJobStep>().SetPropertyValue(s => s.Progress, (long?)progress), cancellationToken);

        await PublishStepAsync(step, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task EndStepAsync(int order, StoreJobStepStatus status, string message = null, CancellationToken cancellationToken = default)
    {
        if (!_steps.TryGetValue(order, out var step))
            return;

        step.Status = status;
        step.EndDate = Now();
        step.Message = SecretRedactor.RedactAndTrim(message, StoreJobStep.MessageMaxLength) ?? step.Message;

        var builder = new SetPropertyBuilder<StoreJobStep>().SetPropertyValue(s => s.Status, status)
                                                            .SetPropertyValue(s => s.EndDate, step.EndDate)
                                                            .SetPropertyValue(s => s.Message, step.Message);

        await _services.StepRepository.ExecuteUpdateAsync(step.Id, builder, cancellationToken);

        await PublishStepAsync(step, cancellationToken);

        await RenewAndCheckCancelAsync(force: true, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OpenedStoreConnection> OpenConnectionAsync(int connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await _services.ConnectionRepository.GetFirstOrDefaultAsync(c => c.Id == connectionId && c.OrganizationId == _job.OrganizationId,
                                                                                     projection: c => new { c.Id, c.Store, c.Name, c.EncryptedSecret, c.EncryptedDataKey, c.KeyVersion },
                                                                                     cancellationToken: cancellationToken);

        if (connection == null || connection.EncryptedSecret == null || connection.EncryptedSecret.Length == 0)
            return null;

        var secret = _services.SecretProtector.Unprotect(new ProtectedSecret(connection.EncryptedSecret, connection.EncryptedDataKey, connection.KeyVersion),
                                                         StoreSecretContext.Connection(_job.OrganizationId, connection.Store));

        try
        {
            return new OpenedStoreConnection(connection.Id, connection.Store, connection.Name, Encoding.UTF8.GetString(secret));
        }
        finally
        {
            Array.Clear(secret);
        }
    }

    private async Task RenewAndCheckCancelAsync(bool force, CancellationToken cancellationToken)
    {
        var now = Now();

        if (!force && now - _lastCancelCheck < CancelCheckInterval)
            return;

        _lastCancelCheck = now;

        await _services.JobRepository.ExecuteUpdateAsync(_job.Id,
                                                         new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.LockedUntil, (DateTime?)now.Add(_services.Lease)),
                                                         cancellationToken);

        var cancelRequested = await _services.JobRepository.GetFirstOrDefaultAsync(j => j.Id == _job.Id, projection: j => j.CancelRequested, cancellationToken: cancellationToken);

        if (!cancelRequested)
            return;

        CancelObserved = true;

        throw new OperationCanceledException("The job was cancelled.");
    }

    private Task PublishStepAsync(StoreJobStep step, CancellationToken cancellationToken) => _services.ActivityPublisher.PublishAsync(new ActivityEventDto
    {
        Type = ActivityEventTypes.JobStep,
        OrganizationId = _job.OrganizationId,
        JobId = _job.Id,
        JobKind = _job.Kind,
        JobStatus = StoreJobStatus.Running,
        Attempt = _job.Attempt,
        Store = _job.Store,
        AppId = _job.AppId,
        StepOrder = step.Order,
        StepName = step.Name,
        StepStatus = step.Status,
        Direction = step.Direction,
        Progress = step.Progress,
        Total = step.Total,
        Message = step.Message,
        Date = Now(),
    }, cancellationToken);

    private DateTime Now() => _services.TimeProvider.GetUtcNow().UtcDateTime;

    private static string Trim(string value, int maxLength) => value?.Length > maxLength ? value[..maxLength] : value;
}

/// <summary>
/// What a <see cref="StoreJobContext"/> uses from the executor's scope.
/// </summary>
/// <param name="JobRepository"></param>
/// <param name="StepRepository"></param>
/// <param name="ConnectionRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="ActivityPublisher"></param>
/// <param name="TimeProvider"></param>
/// <param name="Lease"></param>
public sealed record StoreJobContextServices(IStoreasyRepositoryBase<StoreJob> JobRepository,
                                             IStoreasyRepositoryBase<StoreJobStep> StepRepository,
                                             IStoreasyRepositoryBase<Domain.Stores.StoreConnection> ConnectionRepository,
                                             ISecretProtector SecretProtector,
                                             IActivityPublisher ActivityPublisher,
                                             TimeProvider TimeProvider,
                                             TimeSpan Lease);
