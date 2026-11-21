using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where the effect day of a change event comes from. Values are explicit and never reused.
/// </summary>
public enum ImpactDateSource : byte
{
    /// <summary>The day the store made it live (version ready for sale, submission published).</summary>
    [Description("Live in the store")]
    StoreLive = 1,

    /// <summary>The day the store accepted storeasy's write (a change that is live at once, or whose live day is not known).</summary>
    [Description("Applied")]
    Applied = 2,

    /// <summary>Written to a version that is not live yet: no effect can be measured until it is released.</summary>
    [Description("Awaiting release")]
    AwaitingRelease = 3,

    /// <summary>The day an experiment started or ended.</summary>
    [Description("Experiment")]
    Experiment = 4,
}
