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
    /// Channels of <paramref name="kind"/> when the user has no preference row.
    /// </summary>
    public static (bool InApp, bool Email) Default(NotificationType kind) => kind switch
    {
        NotificationType.JobSucceeded or NotificationType.NewReview or NotificationType.VersionState => (true, false),
        _ => (true, true),
    };

    /// <summary>
    /// Applies the lock to a requested or stored preference.
    /// </summary>
    public static (bool InApp, bool Email) Effective(NotificationType kind, bool inApp, bool email) => IsLocked(kind) ? (true, true) : (inApp, email);
}
