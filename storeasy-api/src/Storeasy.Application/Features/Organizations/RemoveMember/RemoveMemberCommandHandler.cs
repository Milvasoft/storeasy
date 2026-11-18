using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.RemoveMember;

/// <summary>
/// Handles <see cref="RemoveMemberCommand"/>.
/// </summary>
/// <param name="MemberRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="RealtimeConnections"></param>
[Log]
public record RemoveMemberCommandHandler(IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                         IOrganizationAccess OrganizationAccess,
                                         ICurrentUser CurrentUser,
                                         IAuditTrail AuditTrail,
                                         IRealtimeConnections RealtimeConnections) : IInterceptable, ICommandHandler<RemoveMemberCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly IRealtimeConnections _realtimeConnections = RealtimeConnections;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _memberRepository.GetFirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganizationId == request.OrganizationId,
                                                                    projection: m => new { m.Id, m.UserId, m.Role, m.CustomRoleId, Permissions = m.CustomRole != null ? m.CustomRole.Permissions : null, m.User.Email },
                                                                    cancellationToken: cancellationToken);

        if (member == null)
            return Response<int>.Error(0, MessageKey.OrganizationMemberNotFound);

        var isSelf = member.UserId == _currentUser.GetRequiredId();

        if (!isSelf && (_organizationAccess.Current == null || !OrgRoleRules.CanRemove(_organizationAccess.Current, OrgAccess.Of(member.Role, member.CustomRoleId, member.Permissions))))
            return Response<int>.Error(0, MessageKey.OrganizationForbidden);

        if (member.Role == OrgRole.Owner)
        {
            var owners = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId && m.Role == OrgRole.Owner,
                                                             projection: m => new { m.Id },
                                                             cancellationToken: cancellationToken);

            if (OrgRoleRules.RemovesLastOwner(owners.Count, member.Role, null))
                return Response<int>.Error(0, MessageKey.OrganizationLastOwner);
        }

        await _memberRepository.ExecuteDeleteAsync(member.Id, cancellationToken: cancellationToken);

        await _organizationAccess.InvalidateAsync(request.OrganizationId, member.UserId);

        await _realtimeConnections.RemoveFromOrganizationAsync(member.UserId, request.OrganizationId);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.MemberRemoved, member.Id.ToString(), isSelf ? $"{member.Email} left." : $"{member.Email} removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(member.Id);
    }
}
