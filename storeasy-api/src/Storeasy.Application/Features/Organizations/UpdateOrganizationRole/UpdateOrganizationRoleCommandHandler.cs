using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationRole;

/// <summary>
/// Handles <see cref="UpdateOrganizationRoleCommand"/>. The cached access of every member holding the role is dropped,
/// so the new permissions apply at once.
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="MemberRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateOrganizationRoleCommandHandler(IStoreasyRepositoryBase<OrganizationRole> RoleRepository,
                                                   IStoreasyRepositoryBase<OrganizationMember> MemberRepository,
                                                   IOrganizationAccess OrganizationAccess,
                                                   IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateOrganizationRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationRole> _roleRepository = RoleRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = MemberRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.GetOrganizationRoleNamesAsync(request.OrganizationId, cancellationToken);
        var role = roles.FirstOrDefault(r => r.Id == request.RoleId);

        if (role == null)
            return Response<int>.Error(0, MessageKey.OrganizationRoleNotFound);

        var permissions = OrgRoleRules.NormalizeCustom(request.Permissions);

        if (_organizationAccess.Current == null || !OrgRoleRules.CanDefine(_organizationAccess.Current, permissions, role.Permissions))
            return Response<int>.Error(0, MessageKey.OrganizationForbidden);

        var name = request.Name.Trim();

        if (roles.Any(r => r.Id != role.Id && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Response<int>.Error(0, MessageKey.OrganizationRoleNameExists);

        await _roleRepository.ExecuteUpdateAsync(role.Id,
                                                 new SetPropertyBuilder<OrganizationRole>().SetPropertyValue(r => r.Name, name)
                                                                                           .SetPropertyValue(r => r.Description, string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim())
                                                                                           .SetPropertyValue(r => r.Permissions, permissions)
                                                                                           .SetPropertyValue(r => r.AudienceRole, OrgRoleRules.AudienceRoleOf(permissions)),
                                                 cancellationToken);

        var holders = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId && m.CustomRoleId == role.Id,
                                                          projection: m => new OrganizationMember { UserId = m.UserId },
                                                          cancellationToken: cancellationToken);

        await _organizationAccess.InvalidateManyAsync(request.OrganizationId, [.. holders.Select(m => m.UserId)]);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.OrganizationRoleUpdated,
                                     role.Id.ToString(),
                                     $"Custom role '{name}' updated.",
                                     details: new { Before = new { role.Name, Permissions = OrgRoleRules.NormalizeCustom(role.Permissions) }, After = new { Name = name, Permissions = permissions } },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(role.Id);
    }
}
