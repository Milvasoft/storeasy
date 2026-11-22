using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a Chrome Web Store change plan writes. Every write of the Chrome Web Store API v2 storeasy uses is one kind; the
/// store listing (texts and images), the visibility and the privacy practices have no API and are assisted steps.
/// </summary>
public enum ChromeChangeKind : byte
{
    /// <summary>
    /// Upload a new package (optional: the package already uploaded is submitted otherwise) and submit the item: for review,
    /// staged for a later publish, or at once when the store lets the item skip review. An initial deploy percentage may be
    /// given.
    /// </summary>
    [Description("Submit")]
    Publish = 1,

    /// <summary>
    /// Raise the deploy percentage of the published revision (no review; items with more than 10,000 weekly users).
    /// </summary>
    [Description("Deploy percentage")]
    DeployPercentage = 2,

    /// <summary>
    /// Cancel the pending submission.
    /// </summary>
    [Description("Cancel submission")]
    CancelSubmission = 3,

    /// <summary>
    /// Publish an earlier package again under a new version number, to every user.
    /// </summary>
    [Description("Roll back")]
    Rollback = 4,

    /// <summary>
    /// Publish a staged revision (<c>STAGED</c>) now.
    /// </summary>
    [Description("Publish staged")]
    PublishStaged = 5,
}
