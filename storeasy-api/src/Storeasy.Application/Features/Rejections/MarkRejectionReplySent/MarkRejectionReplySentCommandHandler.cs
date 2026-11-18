using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.MarkRejectionReplySent;

/// <summary>
/// Handles <see cref="MarkRejectionReplySentCommand"/>.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="StepRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
[Log]
public record MarkRejectionReplySentCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                                   IStoreasyRepositoryBase<Domain.Apps.AssistedStep> StepRepository,
                                                   IAuditTrail AuditTrail,
                                                   ICurrentUser CurrentUser,
                                                   TimeProvider TimeProvider) : IInterceptable, ICommandHandler<MarkRejectionReplySentCommand, RejectionReplyDraftDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IStoreasyRepositoryBase<Domain.Apps.AssistedStep> _stepRepository = StepRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<RejectionReplyDraftDto>> Handle(MarkRejectionReplySentCommand request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionCaseNotFound);

        if (rejectionCase.State == RejectionState.Closed)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionCaseClosed);

        var length = RejectionCases.ReplyLength(rejectionCase.ReplyDraft);

        if (length == 0)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionReplyEmpty);

        if (length > RejectionCase.ReplyMaxLength)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionReplyTooLong);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        rejectionCase.ReplySentDate = now;
        rejectionCase.ReplySentByUserName = _currentUser.UserName;

        RejectionCases.AddTimeline(rejectionCase, now, "sent", $"{_currentUser.UserName}: {length} characters", _auditTrail.Channel);

        await _caseRepository.UpdateAsync(rejectionCase, cancellationToken, c => c.ReplySentDate, c => c.ReplySentByUserName, c => c.Timeline);

        if (rejectionCase.Store == StoreKind.Apple)
            await _stepRepository.CompleteReplyStepAsync(request.OrganizationId, rejectionCase.AppId, _currentUser.UserName, _auditTrail.Channel, now, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RejectionReplySent, rejectionCase.Id.ToString(), $"Resolution Center reply of {rejectionCase.VersionString} marked as sent.", rejectionCase.Store, cancellationToken: cancellationToken);

        return Response<RejectionReplyDraftDto>.Success(RejectionCases.ToDto(rejectionCase, null, null, null).ReplyDraft);
    }
}
