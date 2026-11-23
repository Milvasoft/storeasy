namespace Storeasy.Domain.Enums;

/// <summary>
/// Notication types.
/// </summary>
public enum NotificationType : byte
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    OrderApproved,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>A version was rejected; a rejection case was opened.</summary>
    Rejection = 40,

    /// <summary>A Microsoft Store submission failed certification.</summary>
    CertificationFailed = 41,

    /// <summary>A store job the user started finished.</summary>
    JobSucceeded = 42,

    /// <summary>A store job the user started failed.</summary>
    JobFailed = 43,

    /// <summary>A change plan waits for approval.</summary>
    PlanPending = 44,

    /// <summary>A deadline is near (compliance timeline, TestFlight build expiry).</summary>
    Deadline = 45,

    /// <summary>New store reviews arrived.</summary>
    NewReview = 46,

    /// <summary>A review reply could not be published.</summary>
    ReviewReplyFailed = 47,

    /// <summary>Organization invitations and joins.</summary>
    Invitation = 48,

    /// <summary>Security and connection alerts (rejected key, invalid webhook signature, secret expiry). Cannot be turned off.</summary>
    ConnectionAlert = 49,

    /// <summary>A delivered package was rejected.</summary>
    ArtifactRejected = 50,

    /// <summary>A rules catalog change affects an app.</summary>
    RuleChanged = 51,

    /// <summary>The rule watcher opened a catalog proposal (platform admins).</summary>
    RuleProposal = 52,

    /// <summary>An App Store version changed state (in review, approved, ready for sale).</summary>
    VersionState = 53,

    /// <summary>A tracked package changed on its registry (new version, deprecated or yanked version, security advisory).</summary>
    PackageRegistry = 54,
}
