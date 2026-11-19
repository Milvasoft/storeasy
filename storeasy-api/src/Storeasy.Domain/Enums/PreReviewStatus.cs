using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an AI pre-review. Values are explicit and never reused.
/// </summary>
public enum PreReviewStatus : byte
{
    /// <summary>Waiting for the job runner.</summary>
    [Description("Queued")]
    Queued = 1,

    /// <summary>The job is collecting the submission or waiting for the model.</summary>
    [Description("Running")]
    Running = 2,

    /// <summary>The report is ready.</summary>
    [Description("Succeeded")]
    Succeeded = 3,

    /// <summary>No report: the store, the key or the model failed.</summary>
    [Description("Failed")]
    Failed = 4,
}
