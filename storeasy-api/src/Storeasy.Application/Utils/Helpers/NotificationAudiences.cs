namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// The role sets notifications go to.
/// </summary>
public static class NotificationAudiences
{
    /// <summary>
    /// Owners and admins (rejections, deadlines, catalog changes, webhook alerts).
    /// </summary>
    public static List<OrgRole> OwnersAndAdmins => [OrgRole.Owner, OrgRole.Admin];

    /// <summary>
    /// Everyone who edits store content (reviews, TestFlight, version states).
    /// </summary>
    public static List<OrgRole> Editors => [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor];
}
