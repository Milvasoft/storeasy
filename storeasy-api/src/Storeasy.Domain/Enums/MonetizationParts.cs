using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Parts of an app's store products a monetization read covers.
/// </summary>
[Flags]
public enum MonetizationParts : byte
{
    /// <summary>
    /// Groups and products only.
    /// </summary>
    [Description("Products")]
    Products = 1,

    /// <summary>
    /// Localizations of groups and products.
    /// </summary>
    [Description("Localizations")]
    Localizations = 2,

    /// <summary>
    /// Prices per territory.
    /// </summary>
    [Description("Prices")]
    Prices = 4,

    /// <summary>
    /// Offers.
    /// </summary>
    [Description("Offers")]
    Offers = 8,

    /// <summary>
    /// Territory availability.
    /// </summary>
    [Description("Availability")]
    Availability = 16,

    /// <summary>
    /// Review screenshot and the other review details.
    /// </summary>
    [Description("Review")]
    Review = 32,

    /// <summary>
    /// Everything.
    /// </summary>
    [Description("All")]
    All = 63,
}
