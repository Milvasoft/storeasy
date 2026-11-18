using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionText;

/// <summary>
/// Handles <see cref="UpdateRejectionTextCommand"/>.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record UpdateRejectionTextCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                                IAuditTrail AuditTrail,
                                                ICurrentUser CurrentUser,
                                                TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateRejectionTextCommand, RejectionCaseDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<RejectionCaseDto>> Handle(UpdateRejectionTextCommand request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<RejectionCaseDto>.Error(null, MessageKey.RejectionCaseNotFound);

        if (rejectionCase.State == RejectionState.Closed)
            return Response<RejectionCaseDto>.Error(null, MessageKey.RejectionCaseClosed);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var guidelines = GuidelineParser.Parse(request.RawText);

        rejectionCase.RawText = request.RawText.Trim();
        rejectionCase.RawTextByUserName = _currentUser.UserName;
        rejectionCase.RawTextDate = now;
        rejectionCase.Guidelines = guidelines;
        rejectionCase.Checklist = GuidelineParser.BuildChecklist(guidelines, rejectionCase.Checklist);

        RejectionCases.AddTimeline(rejectionCase, now, "text", $"{_currentUser.UserName}: {guidelines.Count} guidelines", _auditTrail.Channel);

        await _caseRepository.UpdateAsync(rejectionCase, cancellationToken, c => c.RawText, c => c.RawTextByUserName, c => c.RawTextDate, c => c.Guidelines, c => c.Checklist, c => c.Timeline);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RejectionTextAdded, rejectionCase.Id.ToString(), $"Rejection text added ({string.Join(", ", guidelines.Select(g => g.Code))}).", rejectionCase.Store, cancellationToken: cancellationToken);

        var dto = RejectionCases.ToDto(rejectionCase, null, null, null);

        return guidelines.Count == 0
            ? Response<RejectionCaseDto>.Success(dto, MessageKey.RejectionNoGuideline, MessageType.Warning)
            : Response<RejectionCaseDto>.Success(dto);
    }
}
