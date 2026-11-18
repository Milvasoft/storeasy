using Storeasy.Domain.Enums;
using System.Collections.Frozen;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// The organization role matrix (docs/urun-plani.md section 6), the custom role rules (phase 8) and the pure rules
/// around role changes. No database.
/// </summary>
public static class OrgRoleRules
{
    private static readonly FrozenDictionary<OrgRole, FrozenSet<OrgPermission>> _matrix = new Dictionary<OrgRole, FrozenSet<OrgPermission>>
    {
        [OrgRole.Owner] = Enum.GetValues<OrgPermission>().ToFrozenSet(),
        [OrgRole.Admin] = new[]
        {
            OrgPermission.View,
            OrgPermission.EditContent,
            OrgPermission.ReleaseTest,
            OrgPermission.ReleaseProduction,
            OrgPermission.SubmitForReview,
            OrgPermission.ReplyReviews,
            OrgPermission.ManageConnections,
            OrgPermission.ManageWebhooks,
            OrgPermission.ManageMembers,
        }.ToFrozenSet(),
        [OrgRole.Editor] = new[]
        {
            OrgPermission.View,
            OrgPermission.EditContent,
            OrgPermission.ReleaseTest,
            OrgPermission.ReplyReviews,
        }.ToFrozenSet(),
        [OrgRole.Viewer] = new[]
        {
            OrgPermission.View,
        }.ToFrozenSet(),

        // The floor of a custom role; its own set comes from OrgAccess.Custom.
        [OrgRole.Custom] = new[]
        {
            OrgPermission.View,
        }.ToFrozenSet(),
    }.ToFrozenDictionary();

    /// <summary>
    /// Whether <paramref name="role"/> grants <paramref name="permission"/>.
    /// </summary>
    public static bool Allows(OrgRole role, OrgPermission permission) => _matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    /// <summary>
    /// The permissions of <paramref name="role"/>, in enum order.
    /// </summary>
    public static IReadOnlyList<OrgPermission> PermissionsOf(OrgRole role)
    {
        if (!_matrix.TryGetValue(role, out var permissions))
        {
            return [];
        }

        return [.. permissions.OrderBy(p => p)];
    }

    /// <summary>
    /// Permissions a custom role may hold: every permission except the owner only
    /// <see cref="OrgPermission.ManageOrganization"/>.
    /// </summary>
    public static IReadOnlyList<OrgPermission> CustomAssignable { get; } = [.. Enum.GetValues<OrgPermission>().Where(p => p != OrgPermission.ManageOrganization)];

    /// <summary>
    /// The stored form of a custom role's permissions: defined values only, no owner only permission, always
    /// <see cref="OrgPermission.View"/>, distinct, in enum order.
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static List<OrgPermission> NormalizeCustom(IEnumerable<OrgPermission> permissions)
        => [.. (permissions ?? []).Append(OrgPermission.View).Where(CustomAssignable.Contains).Distinct().Order()];

    /// <summary>
    /// The seeded role whose notifications a custom role receives: the most privileged of admin, editor and viewer whose
    /// whole permission set the custom role holds.
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static OrgRole AudienceRoleOf(IReadOnlyCollection<OrgPermission> permissions)
    {
        foreach (var role in (OrgRole[])[OrgRole.Admin, OrgRole.Editor])
        {
            if (_matrix[role].All(permissions.Contains))
                return role;
        }

        return OrgRole.Viewer;
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may give <paramref name="target"/> to a member who has <paramref name="current"/>
    /// (null for a new member or an invitation). Owners may do anything. Anyone else needs
    /// <see cref="OrgPermission.ManageMembers"/>, may not touch or create owners, and may neither change a member who can do
    /// more than they can nor give more than they hold.
    /// </summary>
    /// <param name="actor"></param>
    /// <param name="current"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static bool CanAssign(OrgAccess actor, OrgAccess current, OrgAccess target)
    {
        if (actor.Role == OrgRole.Owner)
            return true;

        if (!actor.Allows(OrgPermission.ManageMembers) || current?.Role == OrgRole.Owner || target.Role == OrgRole.Owner)
            return false;

        return (current == null || current.IsWithin(actor)) && target.IsWithin(actor);
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may remove a member who has <paramref name="member"/>: owners may remove anyone;
    /// anyone else needs <see cref="OrgPermission.ManageMembers"/> and may not remove an owner or a member who can do more.
    /// </summary>
    /// <param name="actor"></param>
    /// <param name="member"></param>
    /// <returns></returns>
    public static bool CanRemove(OrgAccess actor, OrgAccess member)
    {
        if (actor.Role == OrgRole.Owner)
            return true;

        return actor.Allows(OrgPermission.ManageMembers) && member.Role != OrgRole.Owner && member.IsWithin(actor);
    }

    /// <summary>
    /// Whether <paramref name="actor"/> may define or change a custom role that grants <paramref name="permissions"/> (and,
    /// for a change, currently grants <paramref name="currentPermissions"/>): it needs
    /// <see cref="OrgPermission.ManageMembers"/> and must hold every permission involved.
    /// </summary>
    /// <param name="actor"></param>
    /// <param name="permissions"></param>
    /// <param name="currentPermissions"></param>
    /// <returns></returns>
    public static bool CanDefine(OrgAccess actor, IEnumerable<OrgPermission> permissions, IEnumerable<OrgPermission> currentPermissions = null)
        => actor.Allows(OrgPermission.ManageMembers) && NormalizeCustom(permissions).Concat(currentPermissions ?? []).All(actor.Allows);

    /// <summary>
    /// A role as the audit log names it: the seeded role, or "Custom #{id}".
    /// </summary>
    /// <param name="role"></param>
    /// <param name="customRoleId"></param>
    /// <returns></returns>
    public static string Describe(OrgRole role, int? customRoleId) => role == OrgRole.Custom ? $"Custom #{customRoleId}" : role.ToString();

    /// <summary>
    /// Whether the change leaves the organization without an owner. <paramref name="ownerCount"/> is the number of
    /// owners before the change.
    /// </summary>
    public static bool RemovesLastOwner(int ownerCount, OrgRole currentRole, OrgRole? newRole) => currentRole == OrgRole.Owner && newRole != OrgRole.Owner && ownerCount <= 1;

    /// <summary>
    /// Normalizes a slug candidate: lower case ASCII letters, digits and single hyphens, trimmed to
    /// <see cref="Organization.SlugMaxLength"/>. Turkish letters are mapped to ASCII.
    /// </summary>
    public static string ToSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var lastWasHyphen = true;

        foreach (var raw in value.Trim().ToLowerInvariant())
        {
            var c = raw switch
            {
                'ı' => 'i',
                'ğ' => 'g',
                'ü' => 'u',
                'ş' => 's',
                'ö' => 'o',
                'ç' => 'c',
                _ => raw,
            };

            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                builder.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > Organization.SlugMaxLength)
        {
            slug = slug[..Organization.SlugMaxLength].TrimEnd('-');
        }

        return slug;
    }
}
