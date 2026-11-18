using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Ef.Transaction;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.DeleteOrganizationRole;

/// <summary>
/// Handles <see cref="DeleteOrganizationRoleCommand"/>. Accepted, revoked and expired invitations that named the role are
/// pointed at the viewer role first (they can no longer be used), then the role row is deleted, in one transaction.
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="InvitationRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
[Transaction]
public record DeleteOrganizationRoleCommandHandler(IStoreasyRepositoryBase<OrganizationRole> RoleRepository,
                                                   IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                                   IStoreasyRepositoryBase<OrganizationInvitation> InvitationRepository,
                                                   IOrganizationAccess OrganizationAccess,
                                                   IAuditTrail AuditTrail,
                                                   TimeProvider TimeProvider) : IInterceptable, ICommandHandler<DeleteOrganizationRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationRole> _roleRepository = RoleRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = InvitationRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetFirstOrDefaultAsync(r => r.Id == request.RoleId && r.OrganizationId == request.OrganizationId,
                                                                projection: r => new OrganizationRole { Id = r.Id, Name = r.Name, Permissions = r.Permissions },
                                                                cancellationToken: cancellationToken);

        if (role == null)
            return Response<int>.Error(0, MessageKey.OrganizationRoleNotFound);

        if (_organizationAccess.Current == null || !OrgRoleRules.CanDefine(_organizationAccess.Current, role.Permissions, role.Permissions))
            return Response<int>.Error(0, MessageKey.OrganizationForbidden);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var heldByMember = await _memberRepository.GetFirstOrDefaultAsync(m => m.CustomRoleId == role.Id, projection: m => (int?)m.Id, cancellationToken: cancellationToken);
        var heldByInvitation = await _invitationRepository.GetFirstOrDefaultAsync(i => i.CustomRoleId == role.Id && i.AcceptDate == null && i.RevokeDate == null && i.ExpiryDate > now,
                                                                                  projection: i => (int?)i.Id,
                                                                                  cancellationToken: cancellationToken);

        if (heldByMember.HasValue || heldByInvitation.HasValue)
            return Response<int>.Error(0, MessageKey.OrganizationRoleInUse);

        await _invitationRepository.ExecuteUpdateAsync(i => i.CustomRoleId == role.Id,
                                                       new SetPropertyBuilder<OrganizationInvitation>().SetPropertyValue(i => i.CustomRoleId, (int?)null)
                                                                                                       .SetPropertyValue(i => i.Role, OrgRole.Viewer),
                                                       cancellationToken);

        await _roleRepository.ExecuteDeleteAsync(r => r.Id == role.Id, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrganizationRoleDeleted, role.Id.ToString(), $"Custom role '{role.Name}' deleted.", cancellationToken: cancellationToken);

        return Response<int>.Success(role.Id);
    }
}
