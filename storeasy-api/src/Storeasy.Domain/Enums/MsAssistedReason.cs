using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Why a Microsoft Store item has to be changed in Partner Center instead of the API.
/// </summary>
public enum MsAssistedReason : byte
{
    /// <summary>
    /// A store-managed consumable add-on: the API answers 409.
    /// </summary>
    [Description("Store-managed consumable")]
    StoreManagedConsumable = 1,

    /// <summary>
    /// The product uses mandatory updates: the API answers 409.
    /// </summary>
    [Description("Mandatory update")]
    MandatoryUpdate = 2,

    /// <summary>
    /// The product is on Pricing Version 2: the API cannot read or write its price.
    /// </summary>
    [Description("Pricing Version 2")]
    PricingV2 = 3,

    /// <summary>
    /// A submission created through the API was changed in Partner Center; the API cannot change or commit it anymore.
    /// </summary>
    [Description("Changed in Partner Center")]
    ManualEdit = 4,
}
