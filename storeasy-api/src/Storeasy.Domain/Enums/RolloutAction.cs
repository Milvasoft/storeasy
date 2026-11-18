using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A change to a staged or phased rollout.
/// </summary>
public enum RolloutAction : byte
{
    /// <summary>
    /// Pause an Apple phased release.
    /// </summary>
    [Description("Pause")]
    Pause = 1,

    /// <summary>
    /// Resume a paused or halted rollout.
    /// </summary>
    [Description("Resume")]
    Resume = 2,

    /// <summary>
    /// Release to everyone.
    /// </summary>
    [Description("Complete")]
    Complete = 3,

    /// <summary>
    /// Change the Play user fraction.
    /// </summary>
    [Description("Set percentage")]
    SetPercentage = 4,

    /// <summary>
    /// Halt a Play rollout.
    /// </summary>
    [Description("Halt")]
    Halt = 5,
}
