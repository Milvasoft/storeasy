using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a catalog rule on a day.
/// </summary>
public enum RuleStatus : byte
{
    /// <summary>
    /// Applies now.
    /// </summary>
    [Description("In force")]
    InForce = 1,

    /// <summary>
    /// Starts to apply later.
    /// </summary>
    [Description("Upcoming")]
    Upcoming = 2,

    /// <summary>
    /// Retired.
    /// </summary>
    [Description("Deprecated")]
    Deprecated = 3,
}
