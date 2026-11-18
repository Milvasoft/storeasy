using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreJobs;

/// <summary>
/// Default <see cref="IStoreJobQueue"/>.
/// </summary>
/// <param name="jobRepository"></param>
/// <param name="currentUser"></param>
/// <param name="auditTrail"></param>
/// <param name="activityPublisher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public class StoreJobQueue(IStoreasyRepositoryBase<StoreJob> jobRepository,
                           ICurrentUser currentUser,
                           IAuditTrail auditTrail,
                           IActivityPublisher activityPublisher,
                           StoreJobOptions options,
                           TimeProvider timeProvider) : IStoreJobQueue
{
    private const string _systemUserName = "system";

    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly IActivityPublisher _activityPublisher = activityPublisher;
    private readonly StoreJobOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<List<long>> EnqueueAsync(IReadOnlyList<StoreJobRequest> requests, CancellationToken cancellationToken)
    {
        if (requests == null || requests.Count == 0)
            return [];

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var isUser = _currentUser.IsAuthenticated && _auditTrail.Channel != ActionChannel.System;

        var jobs = requests.Select(request => new StoreJob
        {
            OrganizationId = request.OrganizationId,
            Kind = request.Kind,
            Store = request.Store,
            AppId = request.AppId,
            Payload = request.Payload == null ? null : JsonSerializer.Serialize(request.Payload),
            Status = StoreJobStatus.Queued,
            Channel = _auditTrail.Channel,
            RequestedByUserId = isUser ? _currentUser.Id : null,
            RequestedByUserName = isUser ? _currentUser.UserName : _systemUserName,
            MaxAttempts = Math.Max(1, request.MaxAttempts ?? _options.DefaultMaxAttempts),
            NextAttemptDate = now,
        }).ToList();

        await _jobRepository.AddRangeAsync(jobs, cancellationToken);

        foreach (var job in jobs)
        {
            await _activityPublisher.PublishAsync(new ActivityEventDto
            {
                Type = ActivityEventTypes.JobQueued,
                OrganizationId = job.OrganizationId,
                JobId = job.Id,
                JobKind = job.Kind,
                JobStatus = job.Status,
                Store = job.Store,
                AppId = job.AppId,
                Date = now,
            }, cancellationToken);
        }

        return [.. jobs.Select(j => j.Id)];
    }
}
