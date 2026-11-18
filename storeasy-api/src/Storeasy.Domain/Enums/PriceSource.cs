using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Where a price shown by storeasy comes from. storeasy never makes a price up: it is the store's own equalization, a
/// member's choice from the store's price points, or the result of Play's price conversion.
/// </summary>
public enum PriceSource : byte
{
    /// <summary>
    /// Equalized by the store from the base territory.
    /// </summary>
    [Description("Store")]
    Store = 1,

    /// <summary>
    /// Chosen or entered by a member.
    /// </summary>
    [Description("Manual")]
    Manual = 2,

    /// <summary>
    /// Calculated by the Play price conversion.
    /// </summary>
    [Description("Converted")]
    Converted = 3,
}
