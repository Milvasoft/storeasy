namespace Storeasy.Application.Utils.PermissionManager;

/// <summary>
/// The platform permissions a user holds, as the web app reads them.
/// </summary>
public static class PlatformPermissions
{
    /// <summary>
    /// Distinct and sorted; <see cref="PermissionCatalog.App.SuperAdmin"/> stands for every permission of the catalog, so it
    /// is expanded to all of them (and kept).
    /// </summary>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static List<string> Expand(IEnumerable<string> permissions)
    {
        var held = (permissions ?? []).ToHashSet(StringComparer.Ordinal);

        if (held.Contains(PermissionCatalog.App.SuperAdmin))
            held.UnionWith(PermissionCatalog.GetPermissionsAndGroups().SelectMany(group => group.Value.Select(permission => $"{group.Key}.{permission.Name}")));

        return [.. held.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether <paramref name="held"/> includes full privilege (<see cref="PermissionCatalog.App.SuperAdmin"/>).
    /// </summary>
    /// <param name="held"></param>
    /// <returns></returns>
    public static bool HasFullPrivilege(IEnumerable<string> held) => held?.Contains(PermissionCatalog.App.SuperAdmin, StringComparer.Ordinal) == true;

    /// <summary>
    /// Whether an actor holding <paramref name="actor"/> may grant (or take away, or act on a holder of)
    /// <paramref name="permissions"/>: a full privilege holder may do anything; anyone else only permissions it holds
    /// itself, and never full privilege.
    /// </summary>
    /// <param name="actor"></param>
    /// <param name="permissions"></param>
    /// <returns></returns>
    public static bool CanGrant(IReadOnlyCollection<string> actor, IEnumerable<string> permissions)
    {
        if (HasFullPrivilege(actor))
            return true;

        return (permissions ?? []).All(p => p != PermissionCatalog.App.SuperAdmin && actor.Contains(p, StringComparer.Ordinal));
    }
}
