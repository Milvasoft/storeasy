namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// Retention and language settings of an organization (design 15.20).
/// </summary>
public class OrganizationSettingsDto
{
    /// <summary>
    /// Days the audit log is kept (90, 180, 365 or 730).
    /// </summary>
    public int AuditRetentionDays { get; set; }

    /// <summary>
    /// Days delivered packages are kept (set with <c>artifacts/settings</c>).
    /// </summary>
    public int ArtifactRetentionDays { get; set; }

    /// <summary>
    /// Days a seen in-app notification is kept (instance wide, fixed).
    /// </summary>
    public int SeenNotificationRetentionDays { get; set; }

    /// <summary>
    /// Days an unseen in-app notification is kept (instance wide, fixed).
    /// </summary>
    public int UnseenNotificationRetentionDays { get; set; }

    /// <summary>
    /// Default language of the store texts; null when not chosen.
    /// </summary>
    public string DefaultLocale { get; set; }
}
