using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupBuilds;

/// <summary>
/// Handles <see cref="UpdateBetaGroupBuildsCommand"/>.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="BuildRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record UpdateBetaGroupBuildsCommandHandler(IStoreasyRepositoryBase<BetaGroup> GroupRepository,
                                                  IStoreasyRepositoryBase<Build> BuildRepository,
                                                  IStoreJobQueue StoreJobQueue,
                                                  IAuditTrail AuditTrail,
                                                  TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateBetaGroupBuildsCommand, long>
{
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(UpdateBetaGroupBuildsCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.GroupId && g.OrganizationId == request.OrganizationId,
                                                                  projection: g => new { g.Id, g.StoreAppId, g.ExternalId, g.Name },
                                                                  cancellationToken: cancellationToken);

        if (group == null)
            return Response<long>.Error(0, MessageKey.BetaGroupNotFound);

        var ids = (request.AddBuildIds ?? []).Concat(request.RemoveBuildIds ?? []).Distinct().ToList();

        var builds = await _buildRepository.GetAllAsync(b => ids.Contains(b.Id) && b.StoreAppId == group.StoreAppId,
                                                        projection: b => new Build { Id = b.Id, ExternalId = b.ExternalId, ProcessingState = b.ProcessingState, ExpiryDate = b.ExpiryDate },
                                                        cancellationToken: cancellationToken);

        if (builds.Count != ids.Count)
            return Response<long>.Error(0, MessageKey.BuildNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var added = builds.Where(b => request.AddBuildIds?.Contains(b.Id) == true).ToList();

        if (added.Any(b => b.IsExpired(now)))
            return Response<long>.Error(0, MessageKey.BuildExpired);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.UpdateBuilds,
            GroupExternalId = group.ExternalId,
            AddBuildExternalIds = [.. added.Select(b => b.ExternalId)],
            RemoveBuildExternalIds = [.. builds.Where(b => request.RemoveBuildIds?.Contains(b.Id) == true).Select(b => b.ExternalId)],
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, null, new UpdateTestFlightPayload(group.StoreAppId, change, null), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaGroupChanged, group.Id.ToString(), $"Beta group '{group.Name}': {change.AddBuildExternalIds.Count} builds added, {change.RemoveBuildExternalIds.Count} removed.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
