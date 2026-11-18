using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A change to a gradual package rollout.
/// </summary>
public enum MsRolloutAction : byte
{
    /// <summary>
    /// Sets the percentage of customers who get the packages.
    /// </summary>
    [Description("Set percentage")]
    SetPercentage = 1,

    /// <summary>
    /// Halts the rollout.
    /// </summary>
    [Description("Halt")]
    Halt = 2,

    /// <summary>
    /// Finalizes the rollout: every customer gets the packages.
    /// </summary>
    [Description("Complete")]
    Complete = 3,
}
