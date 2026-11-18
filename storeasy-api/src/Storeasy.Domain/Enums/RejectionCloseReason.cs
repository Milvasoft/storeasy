using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Why a rejection case was closed. Values are explicit and never reused.
/// </summary>
public enum RejectionCloseReason : byte
{
    /// <summary>The version was submitted again.</summary>
    [Description("Resubmitted")]
    Submission = 1,

    /// <summary>A member closed it (the version was withdrawn, for example).</summary>
    [Description("Closed by hand")]
    Manual = 2,
}
