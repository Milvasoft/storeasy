using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// An action inside an organization. Requests declare the permission they need and the organization access behaviour
/// checks it against the caller's <see cref="OrgRole"/> (see <see cref="Organizations.OrgRoleRules"/>). The same check
/// runs for the web app, the REST API and MCP tools.
/// </summary>
public enum OrgPermission : byte
{
    /// <summary>
    /// See the organization, its apps, listings, jobs and audit log.
    /// </summary>
    [Description("View")]
    View = 1,

    /// <summary>
    /// Change listings, screenshots, mockups, builds on versions, age rating and categories.
    /// </summary>
    [Description("Edit store content")]
    EditContent = 2,

    /// <summary>
    /// Upload bundles and release to test tracks.
    /// </summary>
    [Description("Release to test tracks")]
    ReleaseTest = 3,

    /// <summary>
    /// Release to the Play production track.
    /// </summary>
    [Description("Release to production")]
    ReleaseProduction = 4,

    /// <summary>
    /// Submit an App Store version for review.
    /// </summary>
    [Description("Submit for review")]
    SubmitForReview = 5,

    /// <summary>
    /// Reply to store reviews.
    /// </summary>
    [Description("Reply to reviews")]
    ReplyReviews = 6,

    /// <summary>
    /// Add, verify and remove store connections (API keys, service accounts).
    /// </summary>
    [Description("Manage store connections")]
    ManageConnections = 7,

    /// <summary>
    /// Create and remove webhooks.
    /// </summary>
    [Description("Manage webhooks")]
    ManageWebhooks = 8,

    /// <summary>
    /// Invite members, change roles below owner, remove members.
    /// </summary>
    [Description("Manage members")]
    ManageMembers = 9,

    /// <summary>
    /// Assign owners, rename and delete the organization.
    /// </summary>
    [Description("Manage organization")]
    ManageOrganization = 10,
}
