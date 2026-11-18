namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// One role of an organization as the roles screen shows it: the four locked seeded roles (no <see cref="Id"/>, the
/// client names them from <see cref="Role"/>) and the organization's custom roles.
/// </summary>
public class OrgRoleDto
{
    /// <summary>
    /// Custom role id; null for a seeded role.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// The seeded role, or <see cref="OrgRole.Custom"/>.
    /// </summary>
    public OrgRole Role { get; set; }

    /// <summary>
    /// Name of the custom role; null for a seeded role.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of the custom role.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Granted permissions, in enum order.
    /// </summary>
    public IReadOnlyList<OrgPermission> Permissions { get; set; }

    /// <summary>
    /// Members holding the role.
    /// </summary>
    public int MemberCount { get; set; }

    /// <summary>
    /// Pending invitations that give the role.
    /// </summary>
    public int InvitationCount { get; set; }

    /// <summary>
    /// Whether the role is a seeded one (name and permissions cannot change).
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// UTC instant the custom role was created.
    /// </summary>
    public DateTime? CreationDate { get; set; }
}
