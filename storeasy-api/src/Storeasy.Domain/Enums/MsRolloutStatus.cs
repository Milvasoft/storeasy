using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a gradual package rollout.
/// </summary>
public enum MsRolloutStatus : byte
{
    /// <summary>
    /// The rollout has not started.
    /// </summary>
    [Description("Not started")]
    NotStarted = 1,

    /// <summary>
    /// The packages reach the given percentage of customers.
    /// </summary>
    [Description("In progress")]
    InProgress = 2,

    /// <summary>
    /// Every customer gets the packages.
    /// </summary>
    [Description("Complete")]
    Complete = 3,

    /// <summary>
    /// Halted; customers who did not get the packages get the fallback submission's packages.
    /// </summary>
    [Description("Halted")]
    Stopped = 4,
}
