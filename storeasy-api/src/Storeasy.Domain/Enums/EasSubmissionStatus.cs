using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an EAS Submit submission (Expo's <c>SubmissionStatus</c>).
/// </summary>
public enum EasSubmissionStatus : byte
{
    /// <summary>
    /// Waiting for its build.
    /// </summary>
    [Description("Waiting for the build")]
    AwaitingBuild = 1,

    /// <summary>
    /// Waiting for a worker.
    /// </summary>
    [Description("In queue")]
    InQueue = 2,

    /// <summary>
    /// Uploading to the store.
    /// </summary>
    [Description("In progress")]
    InProgress = 3,

    /// <summary>
    /// The store took the build.
    /// </summary>
    [Description("Finished")]
    Finished = 4,

    /// <summary>
    /// Failed.
    /// </summary>
    [Description("Errored")]
    Errored = 5,

    /// <summary>
    /// Canceled.
    /// </summary>
    [Description("Canceled")]
    Canceled = 6,
}
