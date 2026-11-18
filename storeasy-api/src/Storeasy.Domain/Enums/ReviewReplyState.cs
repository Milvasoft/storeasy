using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of the developer reply of a store review. Values are explicit and never reused.
/// </summary>
public enum ReviewReplyState : byte
{
    /// <summary>No reply.</summary>
    [Description("No reply")]
    None = 0,

    /// <summary>A reply job is on the way.</summary>
    [Description("Sending")]
    Sending = 1,

    /// <summary>The store shows the reply.</summary>
    [Description("Published")]
    Published = 2,

    /// <summary>The last reply attempt failed.</summary>
    [Description("Failed")]
    Failed = 3,
}
