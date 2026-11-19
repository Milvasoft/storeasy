using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Severity of a pre-review finding. Values are explicit and never reused.
/// </summary>
public enum PreReviewSeverity : byte
{
    /// <summary>A reviewer would reject the submission for it.</summary>
    [Description("Blocker")]
    Blocker = 1,

    /// <summary>A likely reason for a rejection or a request for more information.</summary>
    [Description("Major")]
    Major = 2,

    /// <summary>Worth fixing; unlikely to cause a rejection alone.</summary>
    [Description("Minor")]
    Minor = 3,
}
