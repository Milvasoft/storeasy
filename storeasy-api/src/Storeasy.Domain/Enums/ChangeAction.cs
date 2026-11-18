using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a plan item does to one field.
/// </summary>
public enum ChangeAction : byte
{
    /// <summary>
    /// The field or item is added.
    /// </summary>
    [Description("Create")]
    Create = 1,

    /// <summary>
    /// The field or item changes.
    /// </summary>
    [Description("Update")]
    Update = 2,

    /// <summary>
    /// The field or item is removed.
    /// </summary>
    [Description("Delete")]
    Delete = 3,
}
