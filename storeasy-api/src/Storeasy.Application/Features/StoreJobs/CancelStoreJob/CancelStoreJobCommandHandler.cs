using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ActivityDtos;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreJobs.CancelStoreJob;

/// <summary>
/// Handles <see cref="CancelStoreJobCommand"/>. Both transitions are conditional updates on the current state, so a
/// job the runner claims meanwhile gets the stop request instead of being cancelled under it.
/// </summary>
/// <param name="JobRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="ActivityPublisher"></param>
/// <param name="TimeProvider"></param>
[Log]
public record CancelStoreJobCommandHandler(IStoreasyRepositoryBase<StoreJob> JobRepository,
                                           IAuditTrail AuditTrail,
                                           IActivityPublisher ActivityPublisher,
                                           TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CancelStoreJobCommand, StoreJobStatus>
{
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = JobRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IActivityPublisher _activityPublisher = ActivityPublisher;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<StoreJobStatus>> Handle(CancelStoreJobCommand request, CancellationToken cancellationToken)
    {
        var job = await _jobRepository.GetFirstOrDefaultAsync(j => j.Id == request.JobId && j.OrganizationId == request.OrganizationId,
                                                              projection: j => new { j.Id, j.Kind, j.Store, j.AppId },
                                                              cancellationToken: cancellationToken);

        if (job == null)
            return Response<StoreJobStatus>.Error(default, MessageKey.StoreJobNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var cancelled = await _jobRepository.ExecuteUpdateAsync(j => j.Id == job.Id && j.Status == StoreJobStatus.Queued,
                                                                new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.Status, StoreJobStatus.Cancelled)
                                                                                                  .SetPropertyValue(j => j.EndDate, (DateTime?)now)
                                                                                                  .SetPropertyValue(j => j.LockedUntil, (DateTime?)null),
                                                                cancellationToken);

        var status = StoreJobStatus.Cancelled;

        if (cancelled == 0)
        {
            var requested = await _jobRepository.ExecuteUpdateAsync(j => j.Id == job.Id && j.Status == StoreJobStatus.Running,
                                                                    new SetPropertyBuilder<StoreJob>().SetPropertyValue(j => j.CancelRequested, true),
                                                                    cancellationToken);

            if (requested == 0)
                return Response<StoreJobStatus>.Error(default, MessageKey.StoreJobAlreadyFinished);

            status = StoreJobStatus.Running;
        }

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.JobCancelled, job.Id.ToString(), $"{job.Kind} job {job.Id} cancelled.", job.Store, cancellationToken: cancellationToken);

        if (status == StoreJobStatus.Cancelled)
        {
            await _activityPublisher.PublishAsync(new ActivityEventDto
            {
                Type = ActivityEventTypes.JobCancelled,
                OrganizationId = request.OrganizationId,
                JobId = job.Id,
                JobKind = job.Kind,
                JobStatus = StoreJobStatus.Cancelled,
                Store = job.Store,
                AppId = job.AppId,
                Date = now,
            }, cancellationToken);
        }

        return Response<StoreJobStatus>.Success(status);
    }
}
