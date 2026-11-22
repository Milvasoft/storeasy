using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Review status of the file of a Firefox version (<c>file.status</c> of the version detail). Values are explicit and never
/// reused.
/// </summary>
public enum FirefoxFileStatus : byte
{
    /// <summary>
    /// No file, or a status the API did not name.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Awaiting review (<c>unreviewed</c>). Unlisted versions are signed automatically and usually skip this state.
    /// </summary>
    [Description("Awaiting review")]
    Unreviewed = 1,

    /// <summary>
    /// Approved and signed (<c>public</c>).
    /// </summary>
    [Description("Approved")]
    Public = 2,

    /// <summary>
    /// Rejected or disabled by Mozilla or the developer (<c>disabled</c>).
    /// </summary>
    [Description("Disabled")]
    Disabled = 3,
}
