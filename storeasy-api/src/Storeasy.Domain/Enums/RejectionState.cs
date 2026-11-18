using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of a rejection case. Values are explicit and never reused.
/// </summary>
public enum RejectionState : byte
{
    /// <summary>Being worked on.</summary>
    [Description("Open")]
    Open = 1,

    /// <summary>Resubmitted or closed by hand; read only history.</summary>
    [Description("Closed")]
    Closed = 2,
}
