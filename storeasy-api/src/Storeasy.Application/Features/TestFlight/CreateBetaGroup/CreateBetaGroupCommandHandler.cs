using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.TestFlight.CreateBetaGroup;

/// <summary>
/// Handles <see cref="CreateBetaGroupCommand"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CreateBetaGroupCommandHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository, IStoreJobQueue StoreJobQueue, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CreateBetaGroupCommand, long>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(CreateBetaGroupCommand request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<long>.Error(0, MessageKey.StoreAppNotFound);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.CreateGroup,
            Name = request.Name.Trim(),
            IsInternal = request.IsInternal,
            PublicLinkEnabled = !request.IsInternal && request.PublicLinkEnabled,
            PublicLinkLimit = request.PublicLinkLimit,
            PublicLinkLimitEnabled = !request.IsInternal && request.PublicLinkLimitEnabled,
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, storeApp.AppId, new UpdateTestFlightPayload(storeApp.Id, change, null), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaGroupChanged, storeApp.Id.ToString(), $"Beta group '{change.Name}' requested.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
