using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// State of one fix item of a rejection case. Values are explicit and never reused.
/// </summary>
public enum ChecklistItemStatus : byte
{
    /// <summary>Not started.</summary>
    [Description("To do")]
    Todo = 1,

    /// <summary>Being worked on.</summary>
    [Description("In progress")]
    InProgress = 2,

    /// <summary>Done.</summary>
    [Description("Done")]
    Done = 3,
}
