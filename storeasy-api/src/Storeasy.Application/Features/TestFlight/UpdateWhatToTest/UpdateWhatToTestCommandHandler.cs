using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.TestFlight.UpdateWhatToTest;

/// <summary>
/// Handles <see cref="UpdateWhatToTestCommand"/>. An expired build is read only.
/// </summary>
/// <param name="BuildRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record UpdateWhatToTestCommandHandler(IStoreasyRepositoryBase<Build> BuildRepository, IStoreJobQueue StoreJobQueue, IAuditTrail AuditTrail, TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateWhatToTestCommand, long>
{
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(UpdateWhatToTestCommand request, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.FindAppleBuildAsync(request.OrganizationId, request.BuildId, cancellationToken);

        if (build == null)
            return Response<long>.Error(0, MessageKey.BuildNotFound);

        if (build.IsExpired(_timeProvider.GetUtcNow().UtcDateTime))
            return Response<long>.Error(0, MessageKey.BuildExpired);

        var change = new TestFlightChange
        {
            Operation = TestFlightOperation.WhatToTest,
            BuildExternalId = build.ExternalId,
            Locale = request.Locale.Trim(),
            Text = request.WhatsNew,
        };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, null, new UpdateTestFlightPayload(build.StoreAppId, change, build.Id), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaTextChanged, build.Id.ToString(), $"What to test of {build.Version} ({build.BuildNumber}) in {change.Locale} written.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
