namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// Turns a winget-pkgs pull request (its state, labels and check runs) into a <see cref="WingetPullRequestState"/>. The
/// validation pipeline and the moderators drive the pull request with labels (winget-pkgs doc/ValidationFailureGuide.md
/// and doc/Moderation.md); a waived label (<c>Waived-X</c>) cancels <c>X</c>.
/// </summary>
public static class WingetReviewStates
{
    /// <summary>
    /// Labels that ask the author to act.
    /// </summary>
    public static IReadOnlySet<string> FeedbackLabels { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Needs-Author-Feedback", "Changes-Requested", "Needs-CLA" };

    /// <summary>
    /// Labels of a passed validation.
    /// </summary>
    public static IReadOnlySet<string> PassedLabels { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Validation-Completed", "Azure-Pipeline-Passed", "Manual-Validation-Completed" };

    /// <summary>
    /// Label of a moderator's approval.
    /// </summary>
    public const string ApprovedLabel = "Moderator-Approved";

    /// <summary>
    /// Label the publish pipeline sets after the merge.
    /// </summary>
    public const string PublishedLabel = "Publish-Pipeline-Succeeded";

    private static readonly HashSet<string> _failureLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Validation-Error", "Binary-Validation-Error", "Installation-Validation-Error", "Installation-Verification-Error", "URL-Validation-Error",
        "Installer-Error", "Validation-Hash-Verification-Failed", "Validation-Unattended-Failed", "Validation-Shell-Execute", "Validation-No-Executables",
        "Validation-Unapproved-URL", "Validation-Domain", "Validation-Indirect-URL", "Validation-Submission-Failed", "Validation-Merge-Conflict",
        "Validation-Missing-Dependency", "Validation-Recursive-Dependency", "Validation-SmartScreen", "Possible-Duplicate", "PullRequest-Error",
        "Unexpected-File", "Version-Parameter-Mismatch", "Package-Metadata-Invalid", "Blocked-InstallerType", "Highest-Version-Removal",
        "Last-Version-Removal", "Manifest-Singleton-Deprecated", "Manifest-Version-Deprecated",
    };

    /// <summary>
    /// Whether <paramref name="label"/> reports a validation failure the author must fix (internal errors of the pipeline
    /// are retried by the moderators and do not count).
    /// </summary>
    /// <param name="label"></param>
    /// <returns></returns>
    public static bool IsFailure(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || label.StartsWith("Internal-Error", StringComparison.OrdinalIgnoreCase) || label.StartsWith("Waived-", StringComparison.OrdinalIgnoreCase))
            return false;

        return _failureLabels.Contains(label)
               || label.StartsWith("Error-", StringComparison.OrdinalIgnoreCase)
               || (label.StartsWith("Manifest-", StringComparison.OrdinalIgnoreCase) && label.EndsWith("-Error", StringComparison.OrdinalIgnoreCase))
               || (label.StartsWith("Validation-", StringComparison.OrdinalIgnoreCase) && label.EndsWith("-Error", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The review state of a pull request.
    /// </summary>
    /// <param name="open">Whether GitHub reports the pull request open.</param>
    /// <param name="merged">Whether it was merged.</param>
    /// <param name="draft">Whether it is a draft.</param>
    /// <param name="labels">Its labels.</param>
    /// <param name="checks">Check runs of the head commit.</param>
    /// <returns></returns>
    public static WingetPullRequestState Of(bool open, bool merged, bool draft, IEnumerable<string> labels, IEnumerable<WingetCheck> checks)
    {
        if (merged)
            return WingetPullRequestState.Merged;

        if (!open)
            return WingetPullRequestState.Closed;

        if (draft)
            return WingetPullRequestState.Draft;

        var set = new HashSet<string>(labels ?? [], StringComparer.OrdinalIgnoreCase);
        var active = set.Where(l => !set.Contains("Waived-" + l)).ToList();

        if (active.Any(FeedbackLabels.Contains))
            return WingetPullRequestState.NeedsAuthorFeedback;

        if (active.Any(IsFailure) || (checks ?? []).Any(c => c.Status == "completed" && c.Conclusion is "failure" or "timed_out" or "action_required"))
            return WingetPullRequestState.ValidationFailed;

        if (set.Contains(ApprovedLabel))
            return WingetPullRequestState.Approved;

        if (active.Any(PassedLabels.Contains))
            return WingetPullRequestState.ValidationCompleted;

        return WingetPullRequestState.Validating;
    }

    /// <summary>
    /// Whether the state waits for the author (the activity board and notifications flag it).
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool NeedsAction(WingetPullRequestState state) => state is WingetPullRequestState.ValidationFailed or WingetPullRequestState.NeedsAuthorFeedback;
}
