using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Role of a user inside one organization. Lower value = more rights. What each role may do is decided in one place:
/// <see cref="Organizations.OrgRoleRules"/>.
/// </summary>
public enum OrgRole : byte
{
    /// <summary>
    /// Full control, including owners, deletion of the organization and everything an admin can do.
    /// </summary>
    [Description("Owner")]
    Owner = 1,

    /// <summary>
    /// Manages members (except owners), store connections, webhooks, review submission and production releases.
    /// </summary>
    [Description("Admin")]
    Admin = 2,

    /// <summary>
    /// Edits listings, screenshots, mockups and test releases; replies to reviews.
    /// </summary>
    [Description("Editor")]
    Editor = 3,

    /// <summary>
    /// Read only.
    /// </summary>
    [Description("Viewer")]
    Viewer = 4,
}
