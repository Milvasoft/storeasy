using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.SubmitBetaReview;

/// <summary>
/// Handles <see cref="SubmitBetaReviewCommand"/>. Only a processed, unexpired build that is not already waiting, in
/// review or approved can be submitted.
/// </summary>
/// <param name="BuildRepository"></param>
/// <param name="BetaBuildRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record SubmitBetaReviewCommandHandler(IStoreasyRepositoryBase<Build> BuildRepository,
                                             IStoreasyRepositoryBase<BetaBuild> BetaBuildRepository,
                                             IStoreJobQueue StoreJobQueue,
                                             IAuditTrail AuditTrail,
                                             TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SubmitBetaReviewCommand, long>
{
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = BuildRepository;
    private readonly IStoreasyRepositoryBase<BetaBuild> _betaBuildRepository = BetaBuildRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SubmitBetaReviewCommand request, CancellationToken cancellationToken)
    {
        var build = await _buildRepository.FindAppleBuildAsync(request.OrganizationId, request.BuildId, cancellationToken);

        if (build == null)
            return Response<long>.Error(0, MessageKey.BuildNotFound);

        if (!request.Confirm || !string.Equals(request.ConfirmVersion?.Trim(), $"{build.Version} ({build.BuildNumber})", StringComparison.Ordinal))
            return Response<long>.Error(0, MessageKey.BetaReviewConfirmRequired);

        if (build.IsExpired(_timeProvider.GetUtcNow().UtcDateTime))
            return Response<long>.Error(0, MessageKey.BuildExpired);

        if (build.ProcessingState != BuildProcessingState.Valid)
            return Response<long>.Error(0, MessageKey.BuildNotReady);

        var state = await _betaBuildRepository.GetFirstOrDefaultAsync(b => b.BuildId == build.Id,
                                                                      projection: b => (BetaReviewState?)b.BetaReviewState,
                                                                      cancellationToken: cancellationToken);

        if (state is BetaReviewState.WaitingForReview or BetaReviewState.InReview or BetaReviewState.Approved)
            return Response<long>.Error(0, MessageKey.BetaReviewAlreadySubmitted);

        var change = new TestFlightChange { Operation = TestFlightOperation.SubmitBetaReview, BuildExternalId = build.ExternalId };

        var jobId = await _storeJobQueue.EnqueueTestFlightAsync(request.OrganizationId, null, new UpdateTestFlightPayload(build.StoreAppId, change, build.Id), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.BetaReviewSubmitted, build.Id.ToString(), $"{build.Version} ({build.BuildNumber}) submitted to beta app review.", StoreKind.Apple, cancellationToken: cancellationToken);

        return Response<long>.Success(jobId);
    }
}
