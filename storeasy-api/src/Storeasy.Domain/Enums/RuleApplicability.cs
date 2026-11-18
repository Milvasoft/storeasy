using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Whether a catalog rule applies to an app.
/// </summary>
public enum RuleApplicability : byte
{
    /// <summary>
    /// Every condition matches.
    /// </summary>
    [Description("Applies")]
    Applies = 1,

    /// <summary>
    /// A condition does not match.
    /// </summary>
    [Description("Does not apply")]
    DoesNotApply = 2,

    /// <summary>
    /// The app data needed by a condition is missing.
    /// </summary>
    [Description("Unknown")]
    Unknown = 3,
}
