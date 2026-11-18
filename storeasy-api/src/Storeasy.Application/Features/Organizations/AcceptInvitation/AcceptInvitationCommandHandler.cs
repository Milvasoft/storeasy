using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.AcceptInvitation;

/// <summary>
/// Handles <see cref="AcceptInvitationCommand"/>. The invitation is consumed with a conditional update first, so two
/// concurrent accepts of the same token cannot both add a membership
/// (<see cref="RepositoryExtensions.JoinOrganizationAsync"/>).
/// </summary>
/// <param name="InvitationRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="UserRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record AcceptInvitationCommandHandler(IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                             IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                             IStoreasyRepositoryBase<User> UserRepository,
                                             ICurrentUser CurrentUser,
                                             IOrganizationAccess OrganizationAccess,
                                             IAuditTrail AuditTrail,
                                             TimeProvider TimeProvider) : IInterceptable, ICommandHandler<AcceptInvitationCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IStoreasyRepositoryBase<User> _userRepository = UserRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = SecureTokens.Hash(request.Token);

        var invitation = await _invitationRepository.GetFirstOrDefaultAsync(i => i.TokenHash == tokenHash && i.AcceptDate == null && i.RevokeDate == null && i.ExpiryDate > now && !i.Organization.IsDeleted,
                                                                            projection: i => new { i.Id, i.OrganizationId, i.NormalizedEmail, i.Role, i.CustomRoleId },
                                                                            cancellationToken: cancellationToken);

        if (invitation == null)
            return Response<int>.Error(0, MessageKey.InvitationInvalid);

        var userEmail = await _userRepository.GetFirstOrDefaultAsync(u => u.Id == userId, projection: u => u.NormalizedEmail, cancellationToken: cancellationToken);

        if (userEmail != invitation.NormalizedEmail)
            return Response<int>.Error(0, MessageKey.InvitationEmailMismatch);

        var alreadyMember = await _memberRepository.GetFirstOrDefaultAsync(m => m.OrganizationId == invitation.OrganizationId && m.UserId == userId,
                                                                           projection: m => (int?)m.Id,
                                                                           cancellationToken: cancellationToken);

        if (alreadyMember.HasValue)
            return Response<int>.Error(0, MessageKey.OrganizationAlreadyMember);

        var memberId = await _invitationRepository.JoinOrganizationAsync(_memberRepository, invitation.Id, invitation.OrganizationId, userId, invitation.Role, invitation.CustomRoleId, now, cancellationToken);

        if (memberId == null)
            return Response<int>.Error(0, MessageKey.InvitationInvalid);

        await _organizationAccess.InvalidateAsync(invitation.OrganizationId, userId);

        await _auditTrail.WriteAsync(invitation.OrganizationId, AuditAction.MemberJoined, memberId.ToString(), $"{_currentUser.UserName} joined as {invitation.Role}.", cancellationToken: cancellationToken);

        return Response<int>.Success(invitation.OrganizationId);
    }
}
