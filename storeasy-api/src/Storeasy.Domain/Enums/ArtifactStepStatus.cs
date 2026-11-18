using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a processing step. Values are explicit and never reused.
/// </summary>
public enum ArtifactStepStatus : byte
{
    /// <summary>
    /// Not started.
    /// </summary>
    [Description("Pending")]
    Pending = 1,

    /// <summary>
    /// In progress.
    /// </summary>
    [Description("Running")]
    Running = 2,

    /// <summary>
    /// Finished.
    /// </summary>
    [Description("Done")]
    Done = 3,

    /// <summary>
    /// Failed.
    /// </summary>
    [Description("Failed")]
    Failed = 4,

    /// <summary>
    /// Not run.
    /// </summary>
    [Description("Skipped")]
    Skipped = 5,
}
