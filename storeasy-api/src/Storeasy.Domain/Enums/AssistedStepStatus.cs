using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an assisted step.
/// </summary>
public enum AssistedStepStatus : byte
{
    /// <summary>
    /// Not started.
    /// </summary>
    [Description("To do")]
    Todo = 1,

    /// <summary>
    /// Some checklist items are done.
    /// </summary>
    [Description("In progress")]
    InProgress = 2,

    /// <summary>
    /// Handed off to a browser agent.
    /// </summary>
    [Description("Handed off")]
    HandedOff = 3,

    /// <summary>
    /// Recorded as done.
    /// </summary>
    [Description("Done")]
    Done = 4,

    /// <summary>
    /// Recorded as not needed.
    /// </summary>
    [Description("Skipped")]
    Skipped = 5,
}
