using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a change plan.
/// </summary>
public enum ChangePlanStatus : byte
{
    /// <summary>
    /// Waiting for a decision.
    /// </summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>
    /// Approved; the apply job is queued or running.
    /// </summary>
    [Description("Approved")]
    Approved = 2,

    /// <summary>
    /// The store accepted every change.
    /// </summary>
    [Description("Applied")]
    Applied = 3,

    /// <summary>
    /// The apply job failed.
    /// </summary>
    [Description("Failed")]
    Failed = 4,

    /// <summary>
    /// Rejected by a member; nothing was written.
    /// </summary>
    [Description("Rejected")]
    Rejected = 5,

    /// <summary>
    /// Withdrawn; nothing was written.
    /// </summary>
    [Description("Discarded")]
    Discarded = 6,

    /// <summary>
    /// Not decided in time; nothing was written.
    /// </summary>
    [Description("Expired")]
    Expired = 7,
}
