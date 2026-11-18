using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationRole;

/// <summary>
/// Handles <see cref="CreateOrganizationRoleCommand"/>.
/// </summary>
/// <param name="RoleRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CreateOrganizationRoleCommandHandler(IStoreasyRepositoryBase<OrganizationRole> RoleRepository,
                                                   IOrganizationAccess OrganizationAccess,
                                                   IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CreateOrganizationRoleCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationRole> _roleRepository = RoleRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateOrganizationRoleCommand request, CancellationToken cancellationToken)
    {
        var permissions = OrgRoleRules.NormalizeCustom(request.Permissions);

        if (_organizationAccess.Current == null || !OrgRoleRules.CanDefine(_organizationAccess.Current, permissions))
            return Response<int>.Error(0, MessageKey.OrganizationForbidden);

        var name = request.Name.Trim();
        var existing = await _roleRepository.GetOrganizationRoleNamesAsync(request.OrganizationId, cancellationToken);

        if (existing.Count >= OrganizationRole.MaxPerOrganization)
            return Response<int>.Error(0, MessageKey.OrganizationRoleLimitReached);

        if (existing.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Response<int>.Error(0, MessageKey.OrganizationRoleNameExists);

        var role = new OrganizationRole
        {
            OrganizationId = request.OrganizationId,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Permissions = permissions,
            AudienceRole = OrgRoleRules.AudienceRoleOf(permissions),
        };

        await _roleRepository.AddAsync(role, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.OrganizationRoleCreated, role.Id.ToString(), $"Custom role '{name}' created.", details: new { role.Name, role.Permissions }, cancellationToken: cancellationToken);

        return Response<int>.Success(role.Id);
    }
}
