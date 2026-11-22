using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Status of a Firefox add-on on addons.mozilla.org (<c>status</c> of the add-on detail). Values are explicit and never
/// reused.
/// </summary>
public enum FirefoxAddonStatus : byte
{
    /// <summary>
    /// Not read yet, or a status the API did not name.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Approved: the listed add-on is public (<c>public</c>).
    /// </summary>
    [Description("Approved")]
    Public = 1,

    /// <summary>
    /// Awaiting review (<c>nominated</c>).
    /// </summary>
    [Description("Awaiting review")]
    Nominated = 2,

    /// <summary>
    /// The listing is incomplete or has no listed version (<c>incomplete</c>); add-ons with unlisted versions only show it.
    /// </summary>
    [Description("Incomplete")]
    Incomplete = 3,

    /// <summary>
    /// Disabled by Mozilla (<c>disabled</c>).
    /// </summary>
    [Description("Disabled by Mozilla")]
    Disabled = 4,

    /// <summary>
    /// Rejected (<c>rejected</c>).
    /// </summary>
    [Description("Rejected")]
    Rejected = 5,

    /// <summary>
    /// Deleted (<c>deleted</c>).
    /// </summary>
    [Description("Deleted")]
    Deleted = 6,
}
