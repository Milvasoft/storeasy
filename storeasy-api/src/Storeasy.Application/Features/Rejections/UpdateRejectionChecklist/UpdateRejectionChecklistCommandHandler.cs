using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Feedback;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Rejections.UpdateRejectionChecklist;

/// <summary>
/// Handles <see cref="UpdateRejectionChecklistCommand"/>. Assignees must be members of the organization; the done date
/// is set when an item becomes done.
/// </summary>
/// <param name="CaseRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record UpdateRejectionChecklistCommandHandler(IStoreasyRepositoryBase<RejectionCase> CaseRepository,
                                                     IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                                     IAuditTrail AuditTrail,
                                                     ICurrentUser CurrentUser,
                                                     TimeProvider TimeProvider) : IInterceptable, ICommandHandler<UpdateRejectionChecklistCommand, RejectionCaseDto>
{
    private readonly IStoreasyRepositoryBase<RejectionCase> _caseRepository = CaseRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<RejectionCaseDto>> Handle(UpdateRejectionChecklistCommand request, CancellationToken cancellationToken)
    {
        var rejectionCase = await _caseRepository.GetCaseAsync(request.OrganizationId, request.CaseId, cancellationToken);

        if (rejectionCase == null)
            return Response<RejectionCaseDto>.Error(null, MessageKey.RejectionCaseNotFound);

        if (rejectionCase.State == RejectionState.Closed)
            return Response<RejectionCaseDto>.Error(null, MessageKey.RejectionCaseClosed);

        var assignees = request.Items.Where(i => i.AssigneeUserId.HasValue).Select(i => i.AssigneeUserId.Value).Distinct().ToList();

        if (assignees.Count > 0)
        {
            var members = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId && assignees.Contains(m.UserId),
                                                              projection: m => new OrganizationMember { UserId = m.UserId },
                                                              cancellationToken: cancellationToken);

            if (members.Count != assignees.Count)
                return Response<RejectionCaseDto>.Error(null, MessageKey.OrganizationMemberNotFound);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var previous = (rejectionCase.Checklist ?? []).Where(i => i.Id != null).ToDictionary(i => i.Id);

        rejectionCase.Checklist = [.. request.Items.Select(item =>
        {
            var before = item.Id != null ? previous.GetValueOrDefault(item.Id) : null;

            return new RejectionChecklistItem
            {
                Id = before?.Id ?? Guid.NewGuid().ToString("N")[..12],
                GuidelineCode = string.IsNullOrWhiteSpace(item.GuidelineCode) ? null : item.GuidelineCode.Trim(),
                Text = item.Text?.Trim(),
                FixRoute = item.FixRoute,
                AssigneeUserId = item.AssigneeUserId,
                Status = item.Status,
                DoneDate = item.Status == ChecklistItemStatus.Done ? before?.DoneDate ?? now : null,
            };
        })];

        RejectionCases.AddTimeline(rejectionCase, now, "checklist", $"{_currentUser.UserName}: {rejectionCase.Checklist.Count(i => i.Status == ChecklistItemStatus.Done)} / {rejectionCase.Checklist.Count}", _auditTrail.Channel);

        await _caseRepository.UpdateAsync(rejectionCase, cancellationToken, c => c.Checklist, c => c.Timeline);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RejectionChecklistUpdated, rejectionCase.Id.ToString(), "Rejection checklist updated.", rejectionCase.Store, cancellationToken: cancellationToken);

        return Response<RejectionCaseDto>.Success(RejectionCases.ToDto(rejectionCase, null, null, null));
    }
}
