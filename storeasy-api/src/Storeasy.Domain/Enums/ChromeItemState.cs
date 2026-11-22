using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a Chrome Web Store item revision, as the Chrome Web Store API v2 reports it (<c>ItemRevisionStatus.state</c>).
/// Values are explicit and never reused.
/// </summary>
public enum ChromeItemState : byte
{
    /// <summary>
    /// No revision, or a state the API did not name (<c>ITEM_STATE_UNSPECIFIED</c>).
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Submitted and waiting for review (<c>PENDING_REVIEW</c>).
    /// </summary>
    [Description("Pending review")]
    PendingReview = 1,

    /// <summary>
    /// Approved and staged: it is published by a later publish call (<c>STAGED</c>).
    /// </summary>
    [Description("Staged")]
    Staged = 2,

    /// <summary>
    /// Published to everyone the item's visibility allows (<c>PUBLISHED</c>).
    /// </summary>
    [Description("Published")]
    Published = 3,

    /// <summary>
    /// Published to the trusted testers only (<c>PUBLISHED_TO_TESTERS</c>); the visibility is set in the developer dashboard.
    /// </summary>
    [Description("Published to testers")]
    PublishedToTesters = 4,

    /// <summary>
    /// Rejected by the review (<c>REJECTED</c>); the reason is in the developer dashboard.
    /// </summary>
    [Description("Rejected")]
    Rejected = 5,

    /// <summary>
    /// The submission was cancelled (<c>CANCELLED</c>).
    /// </summary>
    [Description("Cancelled")]
    Cancelled = 6,
}
