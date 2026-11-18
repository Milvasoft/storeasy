using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Outcome of one readiness check.
/// </summary>
public enum ReadinessStatus : byte
{
    /// <summary>
    /// Met.
    /// </summary>
    [Description("Ok")]
    Ok = 1,

    /// <summary>
    /// Blocks the submission.
    /// </summary>
    [Description("Missing")]
    Missing = 2,

    /// <summary>
    /// Does not block, should be looked at.
    /// </summary>
    [Description("Warning")]
    Warning = 3,

    /// <summary>
    /// Done outside the API; an assisted step is open.
    /// </summary>
    [Description("Assisted")]
    Assisted = 4,

    /// <summary>
    /// Does not apply to the app.
    /// </summary>
    [Description("Not applicable")]
    NotApplicable = 5,

    /// <summary>
    /// storeasy cannot tell (data missing).
    /// </summary>
    [Description("Unknown")]
    Unknown = 6,
}
