using Storeasy.Domain.Enums;
using System.Collections.Frozen;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// What a member may do in one organization: the role and the permission set it grants. A seeded role brings the matrix
/// of <see cref="OrgRoleRules"/>; a custom role (<see cref="OrgRole.Custom"/>) brings its own normalized set. Every
/// organization check (requests, live events, tokens, change plans) asks this, never the role alone.
/// </summary>
public sealed class OrgAccess
{
    private OrgAccess(OrgRole role, int? customRoleId, FrozenSet<OrgPermission> permissions)
    {
        Role = role;
        CustomRoleId = customRoleId;
        Permissions = permissions;
    }

    /// <summary>
    /// The member's role.
    /// </summary>
    public OrgRole Role { get; }

    /// <summary>
    /// The custom role, when <see cref="Role"/> is <see cref="OrgRole.Custom"/>.
    /// </summary>
    public int? CustomRoleId { get; }

    /// <summary>
    /// Granted permissions.
    /// </summary>
    public IReadOnlySet<OrgPermission> Permissions { get; }

    /// <summary>
    /// Whether anything beyond <see cref="OrgPermission.View"/> is granted (a writing token may be created).
    /// </summary>
    public bool CanWrite => Permissions.Any(p => p != OrgPermission.View);

    /// <summary>
    /// Whether <paramref name="permission"/> is granted.
    /// </summary>
    /// <param name="permission"></param>
    /// <returns></returns>
    public bool Allows(OrgPermission permission) => Permissions.Contains(permission);

    /// <summary>
    /// Whether every permission of this access is also granted by <paramref name="other"/>.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsWithin(OrgAccess other) => Permissions.All(other.Allows);

    /// <summary>
    /// The access of a seeded role.
    /// </summary>
    /// <param name="role"></param>
    /// <returns></returns>
    public static OrgAccess Seeded(OrgRole role) => new(role, null, OrgRoleRules.PermissionsOf(role).ToFrozenSet());

    /// <summary>
    /// The access of a custom role; the permissions are normalized (<see cref="OrgRoleRules.NormalizeCustom"/>), so a stored
    /// set can never grant an owner only permission.
    /// </summary>
    /// <param name="customRoleId"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static OrgAccess Custom(int customRoleId, IEnumerable<OrgPermission> permissions) => new(OrgRole.Custom, customRoleId, OrgRoleRules.NormalizeCustom(permissions).ToFrozenSet());

    /// <summary>
    /// The access of a membership row: the custom role's set when the member holds one, the seeded matrix otherwise.
    /// </summary>
    /// <param name="role"></param>
    /// <param name="customRoleId"></param>
    /// <param name="customPermissions"></param>
    /// <returns></returns>
    public static OrgAccess Of(OrgRole role, int? customRoleId, IEnumerable<OrgPermission> customPermissions)
        => role == OrgRole.Custom && customRoleId.HasValue ? Custom(customRoleId.Value, customPermissions ?? []) : Seeded(role == OrgRole.Custom ? OrgRole.Viewer : role);

    /// <summary>
    /// What a read only token of this member may do: view only.
    /// </summary>
    /// <returns></returns>
    public OrgAccess AsReadOnly() => Seeded(OrgRole.Viewer);
}
