using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a store listing experiment record (App Store states are mapped onto it, the raw state is kept as well).
/// </summary>
public enum AsoExperimentStatus : byte
{
    /// <summary>
    /// Only in storeasy; nothing written to the store yet.
    /// </summary>
    [Description("Draft")]
    Draft = 1,

    /// <summary>
    /// Written to the store and still editable there (App Store: PREPARE_FOR_SUBMISSION).
    /// </summary>
    [Description("Prepared")]
    Prepared = 2,

    /// <summary>
    /// Waiting for or in App Review.
    /// </summary>
    [Description("In review")]
    InReview = 3,

    /// <summary>
    /// Approved by App Review and not running yet (an experiment that can be started, a custom product page that is live).
    /// </summary>
    [Description("Approved")]
    Approved = 4,

    /// <summary>
    /// Running: the experiment gets traffic.
    /// </summary>
    [Description("Running")]
    Running = 5,

    /// <summary>
    /// Rejected by App Review.
    /// </summary>
    [Description("Rejected")]
    Rejected = 6,

    /// <summary>
    /// Stopped before it ended on its own.
    /// </summary>
    [Description("Stopped")]
    Stopped = 7,

    /// <summary>
    /// Finished.
    /// </summary>
    [Description("Completed")]
    Completed = 8,
}
