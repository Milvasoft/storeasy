using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.InviteMember;

/// <summary>
/// Handles <see cref="InviteMemberCommand"/>. A previous pending invitation for the same e-mail is revoked, so only the
/// newest link works.
/// </summary>
/// <param name="InvitationRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record InviteMemberCommandHandler(IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                         IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                         IOrganizationAccess OrganizationAccess,
                                         ICurrentUser CurrentUser,
                                         IAuditTrail AuditTrail,
                                         TimeProvider TimeProvider) : IInterceptable, ICommandHandler<InviteMemberCommand, InvitationCreatedDto>
{
    private const string _tokenPrefix = "inv_";

    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<InvitationCreatedDto>> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        if (!OrgRoleRules.CanAssign(_organizationAccess.CurrentRole.GetValueOrDefault(OrgRole.Viewer), null, request.Role) || request.Role == OrgRole.Owner)
            return Response<InvitationCreatedDto>.Error(null, MessageKey.OrganizationForbidden);

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        var alreadyMember = await _memberRepository.GetFirstOrDefaultAsync(m => m.OrganizationId == request.OrganizationId && m.User.NormalizedEmail == normalizedEmail,
                                                                           projection: m => (int?)m.Id,
                                                                           cancellationToken: cancellationToken);

        if (alreadyMember.HasValue)
            return Response<InvitationCreatedDto>.Error(null, MessageKey.OrganizationAlreadyMember);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _invitationRepository.ExecuteUpdateAsync(i => i.OrganizationId == request.OrganizationId && i.NormalizedEmail == normalizedEmail && i.AcceptDate == null && i.RevokeDate == null,
                                                       new Milvasoft.DataAccess.EfCore.Utils.SetPropertyBuilder<OrganizationInvitation>().SetPropertyValue(i => i.RevokeDate, now),
                                                       cancellationToken);

        var token = SecureTokens.Create(_tokenPrefix);

        var invitation = new OrganizationInvitation
        {
            OrganizationId = request.OrganizationId,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = request.Role,
            TokenHash = SecureTokens.Hash(token),
            ExpiryDate = now.Add(OrganizationInvitation.Lifetime),
            InviterUserId = _currentUser.GetRequiredId(),
        };

        await _invitationRepository.AddAsync(invitation, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.MemberInvited, invitation.Id.ToString(), $"{email} invited as {request.Role}.", cancellationToken: cancellationToken);

        return Response<InvitationCreatedDto>.Success(new InvitationCreatedDto { Id = invitation.Id, Token = token, ExpiryDate = invitation.ExpiryDate });
    }
}
