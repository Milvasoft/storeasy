using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroupTesters;

/// <summary>
/// Handles <see cref="UpdateBetaGroupTestersCommand"/>. No [Log]: the request carries testers' e-mail addresses.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="TesterRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
public record UpdateBetaGroupTestersCommandHandler(IStoreasyRepositoryBase<BetaGroup> GroupRepository,
                                                   IStoreasyRepositoryBase<BetaTester> TesterRepository,
                                                   IStoreJobQueue StoreJobQueue,
                                                   IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateBetaGroupTestersCommand, long>
{
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly IStoreasyRepositoryBase<BetaTester> _testerRepository = TesterRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(UpdateBetaGroupTestersCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.GroupId && g.OrganizationId == request.OrganizationId,
                                                                  projection: g => new { g.Id, g.StoreAppId, g.ExternalId, g.Name },
                                                                  cancellationToken: cancellationToken);

        if (group == null)
            return Response<long>.Error(0, MessageKey.BetaGroupNotFound);

        var removeIds = (request.Remove ?? []).Distinct().ToList();
        var removed = removeIds.Count == 0
            ? []
            : await _testerRepository.GetAllAsync(t => removeIds.Contains(t.Id) && t.StoreAppId == group.StoreAppId,
                                                  projection: t => new BetaTester { Id = t.Id, ExternalId = t.ExternalId },
                                                  cancellationToken: cancellationToken);

        if (removed.Count != removeIds.Count)
            return Response<long>.Error(0, MessageKey.BetaTesterNotFound);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.UpdateTesters,
            GroupExternalId = group.ExternalId,
            AddTesters = [.. (request.Add ?? []).Select(t => new BetaTesterInvite(t.Email.Trim(), t.FirstName?.Trim(), t.LastName?.Trim())).DistinctBy(t => t.Email.ToLowerInvariant())],
            RemoveTesterExternalIds = [.. removed.Select(t => t.ExternalId)],
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, null, new UpdateTestFlightPayload(group.StoreAppId, change, null), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaTestersChanged, group.Id.ToString(), $"Beta group '{group.Name}': {change.AddTesters.Count} testers invited, {change.RemoveTesterExternalIds.Count} removed.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
