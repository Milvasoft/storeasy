namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Defaults of the notification kinds: which channels a user gets when they never changed a preference, and which kinds
/// cannot be turned off (security and connection alerts).
/// </summary>
public static class NotificationKinds
{
    /// <summary>
    /// Kinds shown in the preferences screen, in order.
    /// </summary>
    public static IReadOnlyList<NotificationType> Configurable { get; } =
    [
        NotificationType.Rejection,
        NotificationType.CertificationFailed,
        NotificationType.VersionState,
        NotificationType.JobSucceeded,
        NotificationType.JobFailed,
        NotificationType.PlanPending,
        NotificationType.Deadline,
        NotificationType.NewReview,
        NotificationType.ReviewReplyFailed,
        NotificationType.Invitation,
        NotificationType.ArtifactRejected,
        NotificationType.RuleChanged,
        NotificationType.RuleProposal,
        NotificationType.ConnectionAlert,
    ];

    /// <summary>
    /// Whether the user can turn the in-app and e-mail channels of <paramref name="kind"/> off.
    /// </summary>
    public static bool IsLocked(NotificationType kind) => kind == NotificationType.ConnectionAlert;

    /// <summary>
    /// Days a seen in-app notification is kept (<c>notification_cleanup.sql</c>).
    /// </summary>
    public const int SeenRetentionDays = 30;

    /// <summary>
    /// Days an unseen in-app notification is kept (<c>notification_cleanup.sql</c>).
    /// </summary>
    public const int UnseenRetentionDays = 60;

    /// <summary>
    /// Channels of <paramref name="kind"/> when the user has no preference row.
    /// </summary>
    public static (bool InApp, bool Email) Default(NotificationType kind) => kind switch
    {
        NotificationType.JobSucceeded or NotificationType.NewReview or NotificationType.VersionState => (true, false),
        _ => (true, true),
    };

    /// <summary>
    /// Whether a browser push goes out by default (only to users who subscribed a browser): the kinds that also e-mail by
    /// default.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static bool DefaultWebPush(NotificationType kind) => Default(kind).Email;

    /// <summary>
    /// Applies the lock to a requested or stored preference.
    /// </summary>
    public static (bool InApp, bool Email) Effective(NotificationType kind, bool inApp, bool email) => IsLocked(kind) ? (true, true) : (inApp, email);
}
