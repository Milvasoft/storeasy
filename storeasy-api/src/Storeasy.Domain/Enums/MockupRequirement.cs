using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How much a store wants a screenshot class, read from the rules catalog for the export plan. Values are explicit and
/// never reused.
/// </summary>
public enum MockupRequirement : byte
{
    /// <summary>
    /// The store requires the class (selected for export by default).
    /// </summary>
    [Description("Required")]
    Required = 1,

    /// <summary>
    /// The store recommends the class, or will require it from a future date (selected by default).
    /// </summary>
    [Description("Recommended")]
    Recommended = 2,

    /// <summary>
    /// The store accepts the class; another class already covers it (not selected by default).
    /// </summary>
    [Description("Optional")]
    Optional = 3,
}
