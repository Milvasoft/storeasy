using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of one step of a store job.
/// </summary>
public enum StoreJobStepStatus : byte
{
    /// <summary>
    /// In progress.
    /// </summary>
    [Description("Running")]
    Running = 1,

    /// <summary>
    /// Done.
    /// </summary>
    [Description("Succeeded")]
    Succeeded = 2,

    /// <summary>
    /// Ended with an error.
    /// </summary>
    [Description("Failed")]
    Failed = 3,

    /// <summary>
    /// Not needed (nothing to do, or not supported by the store).
    /// </summary>
    [Description("Skipped")]
    Skipped = 4,
}
