using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Review state of a winget-pkgs pull request, read from the pull request itself, its labels and the validation
/// pipeline's checks (docs/connecting-stores.md, winget). A merged pull request is a published version.
/// </summary>
public enum WingetPullRequestState : byte
{
    /// <summary>
    /// Open, the validation pipeline has not finished yet.
    /// </summary>
    [Description("Validating")]
    Validating = 1,

    /// <summary>
    /// The validation pipeline failed (an error label such as <c>Validation-Error</c>,
    /// <c>Manifest-Validation-Error</c>, <c>Error-Hash-Mismatch</c>, or a failed check).
    /// </summary>
    [Description("Validation failed")]
    ValidationFailed = 2,

    /// <summary>
    /// A moderator or the bot asks the author for a change (<c>Needs-Author-Feedback</c>, <c>Changes-Requested</c>,
    /// <c>Needs-CLA</c>); the pull request closes by itself if nobody answers.
    /// </summary>
    [Description("Needs author feedback")]
    NeedsAuthorFeedback = 3,

    /// <summary>
    /// Validation passed (<c>Validation-Completed</c> or <c>Azure-Pipeline-Passed</c>), waiting for a moderator.
    /// </summary>
    [Description("Validation completed")]
    ValidationCompleted = 4,

    /// <summary>
    /// A moderator approved the pull request (<c>Moderator-Approved</c>); it merges next.
    /// </summary>
    [Description("Approved")]
    Approved = 5,

    /// <summary>
    /// Merged: the version is in the winget source once the publish pipeline ran.
    /// </summary>
    [Description("Published")]
    Merged = 6,

    /// <summary>
    /// Closed without a merge (by the author, a moderator or the bot after a feedback timeout).
    /// </summary>
    [Description("Closed")]
    Closed = 7,

    /// <summary>
    /// A draft pull request; the pipeline does not run until it is marked ready for review.
    /// </summary>
    [Description("Draft")]
    Draft = 8,
}
