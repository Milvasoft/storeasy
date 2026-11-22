using Storeasy.Domain.Chrome;

namespace Storeasy.Application.Utils.Chrome;

/// <summary>
/// A change storeasy saw between two status reads of an item.
/// </summary>
/// <param name="Revision"><c>published</c>, <c>submitted</c> or <c>policy</c>.</param>
/// <param name="Previous">State before (for <c>policy</c>: the flags before).</param>
/// <param name="Current">State now.</param>
/// <param name="Version">Version of the revision now (or before, when it went away).</param>
/// <param name="Action">Audit action of the change.</param>
public sealed record ChromeStateTransition(string Revision, string Previous, string Current, string Version, AuditAction Action);

/// <summary>
/// Copies a status read into a <see cref="ChromeItem"/> and names what changed, so the sync and the plan's write record
/// the history the same way.
/// </summary>
public static class ChromeItemStates
{
    /// <summary>
    /// Revision name of the published revision.
    /// </summary>
    public const string Published = "published";

    /// <summary>
    /// Revision name of the submitted revision.
    /// </summary>
    public const string Submitted = "submitted";

    /// <summary>
    /// Name of the policy flags.
    /// </summary>
    public const string Policy = "policy";

    /// <summary>
    /// Copies <paramref name="status"/> into <paramref name="item"/>; returns the changes (none on the first read of an
    /// item, which only fills the history).
    /// </summary>
    /// <param name="item"></param>
    /// <param name="status"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static List<ChromeStateTransition> Merge(ChromeItem item, ChromeItemStatusRead status, DateTime now)
    {
        var first = item.SyncedDate == null;
        var changes = new List<ChromeStateTransition>();
        var history = item.StatusHistory ?? [];

        var publishedState = status.Published?.State ?? ChromeItemState.None;
        var publishedVersion = Trim(status.Published?.Version);
        var submittedState = status.Submitted?.State ?? ChromeItemState.None;
        var submittedVersion = Trim(status.Submitted?.Version);

        if (publishedState != item.PublishedState || publishedVersion != item.PublishedVersion)
        {
            history.Add(new ChromeStateChange { Revision = Published, State = publishedState, Version = publishedVersion, Date = now });

            if (!first)
            {
                var action = publishedState is ChromeItemState.Published or ChromeItemState.PublishedToTesters && publishedVersion != item.PublishedVersion
                    ? AuditAction.ChromePublished
                    : AuditAction.ChromeReviewStateChanged;

                changes.Add(new ChromeStateTransition(Published, item.PublishedState.ToString(), publishedState.ToString(), publishedVersion ?? item.PublishedVersion, action));
            }
        }

        if (submittedState != item.SubmittedState || submittedVersion != item.SubmittedVersion)
        {
            history.Add(new ChromeStateChange { Revision = Submitted, State = submittedState, Version = submittedVersion, Date = now });

            // A submission that disappears because it was published is told by the published revision.
            var publishedNow = submittedState == ChromeItemState.None && item.SubmittedVersion != null && item.SubmittedVersion == publishedVersion;

            if (!first && !publishedNow)
            {
                var action = submittedState switch
                {
                    ChromeItemState.Rejected => AuditAction.ChromeRejected,
                    ChromeItemState.Published or ChromeItemState.PublishedToTesters => AuditAction.ChromePublished,
                    _ => AuditAction.ChromeReviewStateChanged,
                };

                changes.Add(new ChromeStateTransition(Submitted, item.SubmittedState.ToString(), submittedState.ToString(), submittedVersion ?? item.SubmittedVersion, action));
            }
        }

        var flags = Flags(status.Warned, status.TakenDown);
        var previousFlags = Flags(item.Warned, item.TakenDown);

        if (!first && flags != previousFlags)
            changes.Add(new ChromeStateTransition(Policy, previousFlags, flags, publishedVersion, AuditAction.ChromePolicyFlagChanged));

        item.PublishedState = publishedState;
        item.PublishedVersion = publishedVersion;
        item.PublishedDeployPercentage = status.Published?.DeployPercentage;
        item.SubmittedState = submittedState;
        item.SubmittedVersion = submittedVersion;
        item.SubmittedDeployPercentage = status.Submitted?.DeployPercentage;
        item.LastUploadState = status.LastUploadState;
        item.Warned = status.Warned;
        item.TakenDown = status.TakenDown;
        item.IsPending = submittedState == ChromeItemState.PendingReview || status.LastUploadState == ChromeUploadState.InProgress;
        item.StatusHistory = [.. history.TakeLast(100)];
        item.SyncedDate = now;
        item.SyncError = null;

        return changes;
    }

    /// <summary>
    /// Text of the policy flags (<c>ok</c>, <c>warned</c>, <c>taken down</c>).
    /// </summary>
    /// <param name="warned"></param>
    /// <param name="takenDown"></param>
    /// <returns></returns>
    public static string Flags(bool warned, bool takenDown) => takenDown ? "taken down" : warned ? "warned" : "ok";

    private static string Trim(string version) => version?.Length > ChromeItem.VersionMaxLength ? version[..ChromeItem.VersionMaxLength] : version;
}
