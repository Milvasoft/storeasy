using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a store job. Queued and Running are open; the others are final. Values are explicit and never reused (the
/// runner's claim query compares the numbers).
/// </summary>
public enum StoreJobStatus : byte
{
    /// <summary>
    /// Waiting for a runner (new, or a retry waiting for its next attempt).
    /// </summary>
    [Description("Queued")]
    Queued = 1,

    /// <summary>
    /// A runner holds the job.
    /// </summary>
    [Description("Running")]
    Running = 2,

    /// <summary>
    /// Finished successfully.
    /// </summary>
    [Description("Succeeded")]
    Succeeded = 3,

    /// <summary>
    /// Finished with an error after the last attempt.
    /// </summary>
    [Description("Failed")]
    Failed = 4,

    /// <summary>
    /// Stopped by a member before it finished.
    /// </summary>
    [Description("Cancelled")]
    Cancelled = 5,
}
