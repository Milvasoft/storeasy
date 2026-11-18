using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.UpdateOrganizationRole;

/// <summary>
/// Renames a custom role and replaces its permissions. The caller must hold every permission the role grants now and
/// will grant; the members holding it get the new set on their next request. Seeded roles are not custom roles and
/// cannot be changed.
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record UpdateOrganizationRoleCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Custom role id.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Role name, unique in the organization.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Granted permissions.
    /// </summary>
    public List<OrgPermission> Permissions { get; set; }
}
