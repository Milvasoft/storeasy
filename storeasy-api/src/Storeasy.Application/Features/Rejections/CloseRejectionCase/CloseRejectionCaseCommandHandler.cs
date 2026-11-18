using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.CloseRejectionCase;

/// <summary>
/// Handles <see cref="CloseRejectionCaseCommand"/>.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="OrgWebhookPublisher"></param>
/// <param name="TimeProvider"></param>
[Log]
public record CloseRejectionCaseCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                              IAuditTrail AuditTrail,
                                              ICurrentUser CurrentUser,
                                              IOrgWebhookPublisher OrgWebhookPublisher,
                                              TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CloseRejectionCaseCommand, int>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IOrgWebhookPublisher _orgWebhookPublisher = OrgWebhookPublisher;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CloseRejectionCaseCommand request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<int>.Error(0, MessageKey.RejectionCaseNotFound);

        if (rejectionCase.State == RejectionState.Closed)
            return Response<int>.Error(0, MessageKey.RejectionCaseClosed);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        rejectionCase.State = RejectionState.Closed;
        rejectionCase.ClosedDate = now;
        rejectionCase.ClosedVia = RejectionCloseReason.Manual;
        rejectionCase.CloseNote = request.Note?.Trim();

        RejectionCases.AddTimeline(rejectionCase, now, "closed", $"{_currentUser.UserName}: {rejectionCase.CloseNote}", _auditTrail.Channel);

        await _caseRepository.UpdateAsync(rejectionCase, cancellationToken, c => c.State, c => c.ClosedDate, c => c.ClosedVia, c => c.CloseNote, c => c.Timeline);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RejectionClosed, rejectionCase.Id.ToString(), $"Rejection case of {rejectionCase.VersionString} closed by hand.", rejectionCase.Store, cancellationToken: cancellationToken);

        await _orgWebhookPublisher.EnqueueAsync(request.OrganizationId, OrgWebhookEventTypes.RejectionClosed, new { caseId = rejectionCase.Id, rejectionCase.AppId, rejectionCase.VersionString, closedVia = RejectionCloseReason.Manual.ToString() }, cancellationToken);

        return Response<int>.Success(rejectionCase.Id);
    }
}
