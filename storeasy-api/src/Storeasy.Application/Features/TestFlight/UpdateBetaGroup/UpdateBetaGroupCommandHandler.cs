using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.UpdateBetaGroup;

/// <summary>
/// Handles <see cref="UpdateBetaGroupCommand"/>.
/// </summary>
/// <param name="GroupRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateBetaGroupCommandHandler(IStoreasyRepositoryBase<BetaGroup> GroupRepository, IStoreJobQueue StoreJobQueue, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateBetaGroupCommand, long>
{
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = GroupRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(UpdateBetaGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetFirstOrDefaultAsync(g => g.Id == request.GroupId && g.OrganizationId == request.OrganizationId,
                                                                  projection: g => new { g.Id, g.StoreAppId, g.ExternalId, g.Name, g.IsInternal },
                                                                  cancellationToken: cancellationToken);

        if (group == null)
            return Response<long>.Error(0, MessageKey.BetaGroupNotFound);

        if (group.IsInternal && (request.PublicLinkEnabled == true || request.PublicLinkLimitEnabled == true))
            return Response<long>.Error(0, MessageKey.BetaGroupInternalNoLink);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.UpdateGroup,
            GroupExternalId = group.ExternalId,
            Name = request.Name?.Trim(),
            PublicLinkEnabled = request.PublicLinkEnabled,
            PublicLinkLimit = request.PublicLinkLimit,
            PublicLinkLimitEnabled = request.PublicLinkLimitEnabled,
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, null, new UpdateTestFlightPayload(group.StoreAppId, change, null), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaGroupChanged, group.Id.ToString(), $"Beta group '{group.Name}' change requested.", StoreKind.Apple, details: new { request.Name, request.PublicLinkEnabled, request.PublicLinkLimit, request.PublicLinkLimitEnabled }, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
