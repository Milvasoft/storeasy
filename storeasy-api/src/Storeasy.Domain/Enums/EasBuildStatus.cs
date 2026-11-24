using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an EAS build (Expo's <c>BuildStatus</c>), plus the states of a build storeasy asked for before Expo created it.
/// </summary>
public enum EasBuildStatus : byte
{
    /// <summary>
    /// storeasy asked Expo to build from GitHub; Expo has not created the build yet.
    /// </summary>
    [Description("Requested")]
    Requested = 1,

    /// <summary>
    /// Created, not queued yet.
    /// </summary>
    [Description("New")]
    New = 2,

    /// <summary>
    /// Waiting for a worker.
    /// </summary>
    [Description("In queue")]
    InQueue = 3,

    /// <summary>
    /// Building.
    /// </summary>
    [Description("In progress")]
    InProgress = 4,

    /// <summary>
    /// Being canceled.
    /// </summary>
    [Description("Canceling")]
    PendingCancel = 5,

    /// <summary>
    /// Built; the artifact is ready.
    /// </summary>
    [Description("Finished")]
    Finished = 6,

    /// <summary>
    /// Failed.
    /// </summary>
    [Description("Errored")]
    Errored = 7,

    /// <summary>
    /// Canceled.
    /// </summary>
    [Description("Canceled")]
    Canceled = 8,

    /// <summary>
    /// Expo could not start the build from GitHub (for example no GitHub integration or an unknown profile).
    /// </summary>
    [Description("Not started")]
    NotStarted = 9,
}
