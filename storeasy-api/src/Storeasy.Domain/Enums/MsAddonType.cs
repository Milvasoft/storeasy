using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Product type of a Microsoft Store add-on.
/// </summary>
public enum MsAddonType : byte
{
    /// <summary>
    /// Durable: bought once (or for a lifetime).
    /// </summary>
    [Description("Durable")]
    Durable = 1,

    /// <summary>
    /// Consumable (developer managed).
    /// </summary>
    [Description("Consumable")]
    Consumable = 2,

    /// <summary>
    /// Subscription add-on.
    /// </summary>
    [Description("Subscription")]
    Subscription = 3,

    /// <summary>
    /// A type the API answered that storeasy does not know.
    /// </summary>
    [Description("Unknown")]
    Unknown = 4,
}
