using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionReplyDraft;

/// <summary>
/// Handles <see cref="UpdateRejectionReplyDraftCommand"/>. Saved often (the editor saves while typing), so it writes no
/// audit entry; the timeline gets one line per author.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record UpdateRejectionReplyDraftCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                                      IAuditTrail AuditTrail,
                                                      ICurrentUser CurrentUser,
                                                      TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateRejectionReplyDraftCommand, RejectionReplyDraftDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<RejectionReplyDraftDto>> Handle(UpdateRejectionReplyDraftCommand request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionCaseNotFound);

        if (rejectionCase.State == RejectionState.Closed)
            return Response<RejectionReplyDraftDto>.Error(null, MessageKey.RejectionCaseClosed);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (rejectionCase.ReplyDraftByUserName != _currentUser.UserName || rejectionCase.ReplyDraftChannel != _auditTrail.Channel)
            RejectionCases.AddTimeline(rejectionCase, now, "draft", _currentUser.UserName, _auditTrail.Channel);

        rejectionCase.ReplyDraft = request.Body ?? string.Empty;
        rejectionCase.ReplyDraftByUserName = _currentUser.UserName;
        rejectionCase.ReplyDraftChannel = _auditTrail.Channel;
        rejectionCase.ReplyDraftDate = now;

        await _caseRepository.UpdateAsync(rejectionCase, cancellationToken, c => c.ReplyDraft, c => c.ReplyDraftByUserName, c => c.ReplyDraftChannel, c => c.ReplyDraftDate, c => c.Timeline);

        return Response<RejectionReplyDraftDto>.Success(RejectionCases.ToDto(rejectionCase, null, null, null).ReplyDraft);
    }
}
