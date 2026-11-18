using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Result of the last fetch of a watched official source. Values are explicit and never reused.
/// </summary>
public enum RuleSourceStatus : byte
{
    /// <summary>Never fetched.</summary>
    [Description("Not fetched")]
    New = 1,

    /// <summary>Content unchanged.</summary>
    [Description("Unchanged")]
    Unchanged = 2,

    /// <summary>Content changed; a proposal was opened.</summary>
    [Description("Changed")]
    Changed = 3,

    /// <summary>The source could not be read.</summary>
    [Description("Unreachable")]
    Unreachable = 4,
}
