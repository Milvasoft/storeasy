using Storeasy.Domain.Enums;
using System.Collections.Frozen;

namespace Storeasy.Domain.Organizations;

/// <summary>
/// The organization role matrix (docs/urun-plani.md section 6) and the pure rules around role changes. No database.
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
    /// Whether an actor with <paramref name="actorRole"/> may give <paramref name="newRole"/> to a member who has
    /// <paramref name="currentRole"/> (null for a new member or an invitation). Owners may do anything; admins may not
    /// touch owners or create them; nobody else manages members.
    /// </summary>
    public static bool CanAssign(OrgRole actorRole, OrgRole? currentRole, OrgRole newRole)
    {
        if (actorRole == OrgRole.Owner)
        {
            return true;
        }

        if (actorRole != OrgRole.Admin)
        {
            return false;
        }

        return currentRole != OrgRole.Owner && newRole != OrgRole.Owner;
    }

    /// <summary>
    /// Whether an actor with <paramref name="actorRole"/> may remove a member who has <paramref name="memberRole"/>.
    /// </summary>
    public static bool CanRemove(OrgRole actorRole, OrgRole memberRole)
    {
        if (actorRole == OrgRole.Owner)
        {
            return true;
        }

        return actorRole == OrgRole.Admin && memberRole != OrgRole.Owner;
    }

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
