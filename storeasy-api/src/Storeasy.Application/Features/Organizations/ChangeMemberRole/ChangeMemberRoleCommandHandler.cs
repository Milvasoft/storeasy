using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// Handles <see cref="ChangeMemberRoleCommand"/>.
/// </summary>
/// <param name="MemberRepository"></param>
/// <param name="RoleRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
[Log]
public record ChangeMemberRoleCommandHandler(IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                             IStoreasyRepositoryBase<OrganizationRole> RoleRepository,
                                             IOrganizationAccess OrganizationAccess,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<ChangeMemberRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IStoreasyRepositoryBase<OrganizationRole> _roleRepository = RoleRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var member = await _memberRepository.GetFirstOrDefaultAsync(m => m.Id == request.MemberId && m.OrganizationId == request.OrganizationId,
                                                                    projection: m => new { m.Id, m.UserId, m.Role, m.CustomRoleId, Permissions = m.CustomRole != null ? m.CustomRole.Permissions : null, m.User.Email },
                                                                    cancellationToken: cancellationToken);

        if (member == null)
            return Response<int>.Error(0, MessageKey.OrganizationMemberNotFound);

        var target = await _roleRepository.ResolveRoleAsync(request.OrganizationId, request.Role, request.CustomRoleId, cancellationToken);

        if (target == null)
            return Response<int>.Error(0, MessageKey.OrganizationRoleNotFound);

        if (member.Role == target.Role && member.CustomRoleId == target.CustomRoleId)
            return Response<int>.Success(member.Id);

        var current = OrgAccess.Of(member.Role, member.CustomRoleId, member.Permissions);

        if (_organizationAccess.Current == null || !OrgRoleRules.CanAssign(_organizationAccess.Current, current, target))
            return Response<int>.Error(0, MessageKey.OrganizationForbidden);

        if (member.Role == OrgRole.Owner)
        {
            var ownerCount = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId && m.Role == OrgRole.Owner,
                                                                 projection: m => new { m.Id },
                                                                 cancellationToken: cancellationToken);

            if (OrgRoleRules.RemovesLastOwner(ownerCount.Count, member.Role, request.Role))
                return Response<int>.Error(0, MessageKey.OrganizationLastOwner);
        }

        await _memberRepository.ExecuteUpdateAsync(member.Id,
                                                   new SetPropertyBuilder<OrganizationMember>().SetPropertyValue(m => m.Role, target.Role)
                                                                                               .SetPropertyValue(m => m.CustomRoleId, target.CustomRoleId),
                                                   cancellationToken);

        await _organizationAccess.InvalidateAsync(request.OrganizationId, member.UserId);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MemberRoleChanged,
                                     member.Id.ToString(),
                                     $"{member.Email}: {OrgRoleRules.Describe(member.Role, member.CustomRoleId)} -> {OrgRoleRules.Describe(target.Role, target.CustomRoleId)}.",
                                     details: new { From = member.Role, FromCustomRoleId = member.CustomRoleId, To = target.Role, ToCustomRoleId = target.CustomRoleId },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(member.Id);
    }
}
