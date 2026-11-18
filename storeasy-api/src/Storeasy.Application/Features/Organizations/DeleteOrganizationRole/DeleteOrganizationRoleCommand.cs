using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.DeleteOrganizationRole;

/// <summary>
/// Deletes a custom role that no member and no pending invitation holds (move them to another role first). The caller
/// must hold every permission the role grants.
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record DeleteOrganizationRoleCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Custom role id.
    /// </summary>
    public int RoleId { get; set; }
}
