using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Distribution channel of a Firefox version (<c>channel</c>). Values are explicit and never reused.
/// </summary>
public enum FirefoxVersionChannel : byte
{
    /// <summary>
    /// Not known.
    /// </summary>
    [Description("None")]
    None = 0,

    /// <summary>
    /// Listed on addons.mozilla.org: reviewed by Mozilla and distributed through the add-on page (<c>listed</c>).
    /// </summary>
    [Description("Listed")]
    Listed = 1,

    /// <summary>
    /// Unlisted: signed by Mozilla for self distribution, not shown on addons.mozilla.org (<c>unlisted</c>).
    /// </summary>
    [Description("Unlisted")]
    Unlisted = 2,
}
