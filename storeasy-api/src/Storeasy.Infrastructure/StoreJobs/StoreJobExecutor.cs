using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Npgsql;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.Infrastructure.Persistence.Context;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// Claims and runs one store job (scoped; the runner creates a scope per job). The claim is a single
/// <c>UPDATE ... WHERE Id = (SELECT ... FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING</c>, so concurrent workers and
/// instances never get the same job. A queued job whose next attempt is due, or a running job whose lease expired
/// (its instance died), is claimed. The attempt ends in Succeeded, Failed, Cancelled, or Queued again: a transient
/// failure with attempts left (backoff, <see cref="StoreJobRules.RetryDelay"/>) or a host shutdown (the attempt is
/// given back).
/// </summary>
/// <param name="dbContext"></param>
/// <param name="jobRepository"></param>
/// <param name="stepRepository"></param>
/// <param name="connectionRepository"></param>
/// <param name="handlers"></param>
/// <param name="secretProtector"></param>
/// <param name="activityPublisher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public class StoreJobExecutor(StoreasyDbContext dbContext,
                              IStoreasyRepositoryBase<StoreJob> jobRepository,
                              IStoreasyRepositoryBase<StoreJobStep> stepRepository,
                              IStoreasyRepositoryBase<StoreConnection> connectionRepository,
                              IEnumerable<IStoreJobHandler> handlers,
                              ISecretProtector secretProtector,
                              IActivityPublisher activityPublisher,
                              StoreJobOptions options,
                              TimeProvider timeProvider,
                              IMilvaLogger logger)
{
    private static readonly string _claimSql = $"""
                                                UPDATE "{TableNames.StoreJobs}" AS j
                                                SET "Status" = {(byte)StoreJobStatus.Running},
                                                    "Attempt" = j."Attempt" + 1,
                                                    "StartDate" = COALESCE(j."StartDate", @now),
                                                    "LockedUntil" = @lease
                                                WHERE j."Id" = (
                                                    SELECT c."Id" FROM "{TableNames.StoreJobs}" AS c
                                                    WHERE (c."Status" = {(byte)StoreJobStatus.Queued} AND c."NextAttemptDate" <= @now)
                                                       OR (c."Status" = {(byte)StoreJobStatus.Running} AND c."LockedUntil" < @now)
                                                    ORDER BY c."NextAttemptDate", c."Id"
                                                    LIMIT 1
                                                    FOR UPDATE SKIP LOCKED)
                                                RETURNING j."Id" AS "Value"
                                                """;

    private readonly StoreasyDbContext _dbContext = dbContext;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly IStoreasyRepositoryBase<StoreJobStep> _stepRepository = stepRepository;
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = connectionRepository;
    private readonly IEnumerable<IStoreJobHandler> _handlers = handlers;
    private readonly ISecretProtector _secretProtector = secretProtector;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly StoreJobOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <summary>
    /// Claims the next due job and runs one attempt of it.
    /// </summary>
    /// <param name="stoppingToken">Signalled when the host stops.</param>
    /// <returns>Whether a job was claimed.</returns>
    public async Task<bool> RunNextAsync(CancellationToken stoppingToken)
    {
        var now = Now();

        var claimed = await _dbContext.Database.SqlQueryRaw<long>(_claimSql,
                                                                  new NpgsqlParameter("now", now),
                                                                  new NpgsqlParameter("lease", now.AddSeconds(_options.LeaseSeconds)))
                                               .ToListAsync(stoppingToken);

        if (claimed.Count == 0)
            return false;

        await RunAsync(claimed[0], stoppingToken);

        return true;
    }

    private async Task RunAsync(long jobId, CancellationToken stoppingToken)
    {
        var job = await _jobRepository.GetFirstOrDefaultAsync(j => j.Id == jobId,
                                                              projection: j => new StoreJob
                                                              {
                                                                  Id = j.Id,
                                                                  OrganizationId = j.OrganizationId,
                                                                  AppId = j.AppId,
                                                                  Store = j.Store,
                                                                  Kind = j.Kind,
                                                                  Payload = j.Payload,
                                                                  Attempt = j.Attempt,
                                                                  MaxAttempts = j.MaxAttempts,
                                                                  CancelRequested = j.CancelRequested,
                                                                  Steps = j.Steps.OrderByDescending(s => s.Order).Take(1).Select(s => new StoreJobStep { Order = s.Order }).ToList(),
                                                              },
                                                              cancellationToken: CancellationToken.None);

        if (job.CancelRequested)
        {
            await FinishAsync(job, StoreJobStatus.Cancelled, null, null, null);
            return;
        }

        if (job.Attempt > job.MaxAttempts)
        {
            await FinishAsync(job, StoreJobStatus.Failed, null, null, "The runner stopped during the last attempt.");
            return;
        }

        var handler = _handlers.LastOrDefault(h => h.Kind == job.Kind);

        if (handler == null)
        {
            await FinishAsync(job, StoreJobStatus.Failed, null, null, $"No handler for the job kind {job.Kind}.");
            return;
        }

        await PublishAsync(job, ActivityEventTypes.JobProgress, StoreJobStatus.Running, null);

        var firstStepOrder = (job.Steps.FirstOrDefault()?.Order ?? 0) + 1;
        var context = new StoreJobContext(job, firstStepOrder, new StoreJobContextServices(_jobRepository,
                                                                                          _stepRepository,
                                                                                          _connectionRepository,
                                                                                          _secretProtector,
                                                                                          _activityPublisher,
                                                                                          _timeProvider,
                                                                                          TimeSpan.FromSeconds(_options.LeaseSeconds)));

        try
        {
            var outcome = await handler.ExecuteAsync(context, stoppingToken);

            await FinishAsync(job, StoreJobStatus.Succeeded, outcome?.Summary, outcome?.Result, null);
        }
        catch (OperationCanceledException) when (context.CancelObserved)
        {
            await FinishAsync(job, StoreJobStatus.Cancelled, null, null, null);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await GiveBackAsync(job);
        }
        catch (Exception ex)
        {
            await FailAttemptAsync(job, ex);
        }
    }

    /// <summary>
    /// Whether a failed attempt may succeed later: a <see cref="StoreJobException"/> says so itself; network failures and
    /// timeouts are transient; anything else (a bug, bad data) is not.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static bool IsTransient(Exception exception) => exception switch
    {
        StoreJobException storeJobException => storeJobException.Transient,
        HttpRequestException or TimeoutException or TaskCanceledException => true,
        _ => false,
    };

    private async Task FailAttemptAsync(StoreJob job, Exception exception)
    {
        var error = SecretRedactor.RedactAndTrim(exception.Message, StoreJob.ErrorMaxLength);

        if (exception is not StoreJobException)
            _logger.Error(exception, "Store job {JobId} ({Kind}) attempt {Attempt} failed.", job.Id, job.Kind, job.Attempt);

        if (!StoreJobRules.ShouldRetry(job.Attempt, job.MaxAttempts, IsTransient(exception)))
        {
            await FinishAsync(job, StoreJobStatus.Failed, null, null, error);
            return;
        }

        var nextAttempt = Now().Add(StoreJobRules.RetryDelay(job.Attempt, TimeSpan.FromSeconds(_options.RetryBaseDelaySeconds), TimeSpan.FromSeconds(_options.RetryMaxDelaySeconds)));

        await CloseOpenStepsAsync(job.Id, StoreJobStepStatus.Failed);

        await _jobRepository.ExecuteUpdateAsync(job.Id,
                                                new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.Status, StoreJobStatus.Queued)
                                                                                  .SetPropertyValue(j => j.NextAttemptDate, nextAttempt)
                                                                                  .SetPropertyValue(j => j.LockedUntil, (DateTime?)null)
                                                                                  .SetPropertyValue(j => j.Error, error),
                                                CancellationToken.None);

        await PublishAsync(job, ActivityEventTypes.JobProgress, StoreJobStatus.Queued, error);
    }

    private async Task GiveBackAsync(StoreJob job)
    {
        await CloseOpenStepsAsync(job.Id, StoreJobStepStatus.Skipped);

        // The host is stopping: the attempt does not count, another instance (or this one after the restart) runs it.
        await _jobRepository.ExecuteUpdateAsync(job.Id,
                                                new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.Status, StoreJobStatus.Queued)
                                                                                  .SetProperty(j => j.Attempt, j => j.Attempt - 1)
                                                                                  .SetPropertyValue(j => j.NextAttemptDate, Now())
                                                                                  .SetPropertyValue(j => j.LockedUntil, (DateTime?)null),
                                                CancellationToken.None);
    }

    private async Task FinishAsync(StoreJob job, StoreJobStatus status, string summary, object result, string error)
    {
        var now = Now();

        await CloseOpenStepsAsync(job.Id, status == StoreJobStatus.Cancelled ? StoreJobStepStatus.Skipped : StoreJobStepStatus.Failed);

        var builder = new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.Status, status)
                                                        .SetPropertyValue(j => j.EndDate, (DateTime?)now)
                                                        .SetPropertyValue(j => j.LockedUntil, (DateTime?)null)
                                                        .SetPropertyValue(j => j.Summary, SecretRedactor.RedactAndTrim(summary, StoreJob.SummaryMaxLength))
                                                        .SetPropertyValue(j => j.Result, result == null ? null : SecretRedactor.Redact(JsonSerializer.Serialize(result)))
                                                        .SetPropertyValue(j => j.Error, error);

        await _jobRepository.ExecuteUpdateAsync(job.Id, builder, CancellationToken.None);

        var type = status switch
        {
            StoreJobStatus.Succeeded => ActivityEventTypes.JobSucceeded,
            StoreJobStatus.Cancelled => ActivityEventTypes.JobCancelled,
            _ => ActivityEventTypes.JobFailed,
        };

        await PublishAsync(job, type, status, status == StoreJobStatus.Succeeded ? summary : error);
    }

    private Task<int> CloseOpenStepsAsync(long jobId, StoreJobStepStatus status)
        => _stepRepository.ExecuteUpdateAsync(s => s.StoreJobId == jobId && s.Status == StoreJobStepStatus.Running,
                                              new SetPropertyBuilder<StoreJobStep>().SetPropertyValue(s => s.Status, status)
                                                                                    .SetPropertyValue(s => s.EndDate, (DateTime?)Now()),
                                              CancellationToken.None);

    private Task PublishAsync(StoreJob job, string type, StoreJobStatus status, string message) => _activityPublisher.PublishAsync(new ActivityEventDto
    {
        Type = type,
        OrganizationId = job.OrganizationId,
        JobId = job.Id,
        JobKind = job.Kind,
        JobStatus = status,
        Attempt = job.Attempt,
        Store = job.Store,
        AppId = job.AppId,
        Message = SecretRedactor.RedactAndTrim(message, StoreJobStep.MessageMaxLength),
        Date = Now(),
    }, CancellationToken.None);

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
