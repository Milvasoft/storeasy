using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a store product sold inside an app is, store neutral (App Store subscriptions and In-App Purchases, Play
/// subscriptions and one-time products).
/// </summary>
public enum StoreProductKind : byte
{
    /// <summary>
    /// Auto-renewable subscription (App Store subscription in a group, Play subscription with base plans).
    /// </summary>
    [Description("Subscription")]
    Subscription = 1,

    /// <summary>
    /// Consumable In-App Purchase (used up and bought again).
    /// </summary>
    [Description("Consumable")]
    Consumable = 2,

    /// <summary>
    /// Non-consumable In-App Purchase (bought once, restored).
    /// </summary>
    [Description("Non-consumable")]
    NonConsumable = 3,

    /// <summary>
    /// Non-renewing subscription (time limited access that does not renew).
    /// </summary>
    [Description("Non-renewing subscription")]
    NonRenewingSubscription = 4,

    /// <summary>
    /// Play one-time product; whether it is consumed is decided by the app, Play has no type for it.
    /// </summary>
    [Description("One-time product")]
    OneTimeProduct = 5,
}
