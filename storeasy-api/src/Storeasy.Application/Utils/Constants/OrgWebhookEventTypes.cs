namespace Storeasy.Application.Utils.Constants;

/// <summary>
/// Event types an organization's outgoing webhooks can subscribe to (the <c>type</c> of the posted body and the
/// <c>X-Storeasy-Event</c> header).
/// </summary>
public static class OrgWebhookEventTypes
{
    /// <summary>A rejection case was opened.</summary>
    public const string RejectionOpened = "rejection.opened";

    /// <summary>A rejection case was closed.</summary>
    public const string RejectionClosed = "rejection.closed";

    /// <summary>An App Store version changed state.</summary>
    public const string VersionStateChanged = "version.state_changed";

    /// <summary>New store reviews arrived.</summary>
    public const string ReviewReceived = "review.received";

    /// <summary>A review reply was published.</summary>
    public const string ReviewReplied = "review.replied";

    /// <summary>A TestFlight build expires soon.</summary>
    public const string BuildExpiring = "testflight.build.expiring";

    /// <summary>A compliance deadline is near.</summary>
    public const string ComplianceDeadline = "compliance.deadline";

    /// <summary>A test delivery asked for by a member.</summary>
    public const string Test = "webhook.test";

    /// <summary>
    /// Every event type a subscription may name.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [RejectionOpened, RejectionClosed, VersionStateChanged, ReviewReceived, ReviewReplied, BuildExpiring, ComplianceDeadline, Test];
}
