using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.CreateOrganizationRole;

/// <summary>
/// Defines a custom role in the organization. The caller must hold every permission it grants (only owners hold all);
/// the owner only permission is never granted and viewing always is. Returns the role id.
/// </summary>
[OrgAuth(OrgPermission.ManageMembers)]
public record CreateOrganizationRoleCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

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
