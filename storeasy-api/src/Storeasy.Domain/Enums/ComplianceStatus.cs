using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of an app against one deadline of the compliance timeline.
/// </summary>
public enum ComplianceStatus : byte
{
    /// <summary>
    /// The app complies.
    /// </summary>
    [Description("Done")]
    Done = 1,

    /// <summary>
    /// The app does not comply yet.
    /// </summary>
    [Description("Missing")]
    Missing = 2,

    /// <summary>
    /// storeasy cannot tell.
    /// </summary>
    [Description("Unknown")]
    Unknown = 3,

    /// <summary>
    /// The rule does not apply to the app.
    /// </summary>
    [Description("Not applicable")]
    NotApplicable = 4,
}
