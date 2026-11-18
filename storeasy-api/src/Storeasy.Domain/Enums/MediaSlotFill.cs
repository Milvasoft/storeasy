using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How a media class (slot) is filled.
/// </summary>
public enum MediaSlotFill : byte
{
    /// <summary>
    /// The class has its own items.
    /// </summary>
    [Description("Own")]
    Own = 1,

    /// <summary>
    /// The store derives the class from another class.
    /// </summary>
    [Description("Fallback")]
    Fallback = 2,

    /// <summary>
    /// Neither own items nor a fallback.
    /// </summary>
    [Description("None")]
    None = 3,
}
