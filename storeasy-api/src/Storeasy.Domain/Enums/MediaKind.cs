using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of store media.
/// </summary>
public enum MediaKind : byte
{
    /// <summary>
    /// Screenshots and graphics.
    /// </summary>
    [Description("Screenshot")]
    Screenshot = 1,

    /// <summary>
    /// App preview videos.
    /// </summary>
    [Description("App preview")]
    Preview = 2,
}
