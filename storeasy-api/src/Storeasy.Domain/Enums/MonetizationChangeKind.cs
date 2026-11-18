using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kind of one requested change of a monetization plan (subscriptions, offers, In-App Purchases, Play products and the
/// store's purchase settings).
/// </summary>
public enum MonetizationChangeKind : byte
{
    /// <summary>
    /// Create an App Store subscription group (with its first localizations).
    /// </summary>
    [Description("Create group")]
    CreateGroup = 1,

    /// <summary>
    /// Rename a subscription group.
    /// </summary>
    [Description("Update group")]
    UpdateGroup = 2,

    /// <summary>
    /// Add or change a subscription group localization.
    /// </summary>
    [Description("Group localization")]
    GroupLocalization = 3,

    /// <summary>
    /// Create an App Store subscription.
    /// </summary>
    [Description("Create subscription")]
    CreateSubscription = 4,

    /// <summary>
    /// Change the fields of a subscription or In-App Purchase (name, family sharing, review note, multiseat).
    /// </summary>
    [Description("Update product")]
    UpdateProduct = 5,

    /// <summary>
    /// Add or change a localization of a subscription or In-App Purchase.
    /// </summary>
    [Description("Product localization")]
    ProductLocalization = 6,

    /// <summary>
    /// Move a subscription to another level of its group.
    /// </summary>
    [Description("Level")]
    Level = 7,

    /// <summary>
    /// Base territory price; the other territories follow the store's equalization unless set by hand.
    /// </summary>
    [Description("Base price")]
    BasePrice = 8,

    /// <summary>
    /// Price of one territory chosen by hand.
    /// </summary>
    [Description("Territory price")]
    TerritoryPrice = 9,

    /// <summary>
    /// Territories where the product is sold.
    /// </summary>
    [Description("Availability")]
    Availability = 10,

    /// <summary>
    /// App Store introductory offer.
    /// </summary>
    [Description("Introductory offer")]
    IntroductoryOffer = 11,

    /// <summary>
    /// App Store promotional offer.
    /// </summary>
    [Description("Promotional offer")]
    PromotionalOffer = 12,

    /// <summary>
    /// App Store offer code (with custom and one-time codes).
    /// </summary>
    [Description("Offer code")]
    OfferCode = 13,

    /// <summary>
    /// App Store win-back offer.
    /// </summary>
    [Description("Win-back offer")]
    WinBackOffer = 14,

    /// <summary>
    /// Review screenshot from an uploaded asset.
    /// </summary>
    [Description("Review screenshot")]
    ReviewScreenshot = 15,

    /// <summary>
    /// Create an App Store In-App Purchase.
    /// </summary>
    [Description("Create In-App Purchase")]
    CreateIap = 16,

    /// <summary>
    /// Create a Play subscription or change its listings.
    /// </summary>
    [Description("Play subscription")]
    PlaySubscription = 17,

    /// <summary>
    /// Add a Play base plan.
    /// </summary>
    [Description("Base plan")]
    BasePlan = 18,

    /// <summary>
    /// Activate or deactivate a Play base plan.
    /// </summary>
    [Description("Base plan state")]
    BasePlanState = 19,

    /// <summary>
    /// Regional prices of a Play base plan.
    /// </summary>
    [Description("Base plan prices")]
    BasePlanPrices = 20,

    /// <summary>
    /// Create a Play subscription offer.
    /// </summary>
    [Description("Play offer")]
    PlayOffer = 21,

    /// <summary>
    /// Activate or deactivate a Play subscription offer.
    /// </summary>
    [Description("Play offer state")]
    PlayOfferState = 22,

    /// <summary>
    /// Create or change a Play one-time product (listings, purchase option, prices, state).
    /// </summary>
    [Description("One-time product")]
    OneTimeProduct = 23,

    /// <summary>
    /// App Store Server Notifications addresses.
    /// </summary>
    [Description("Server notifications")]
    ServerNotifications = 24,

    /// <summary>
    /// Sandbox tester settings or purchase history.
    /// </summary>
    [Description("Sandbox tester")]
    SandboxTester = 25,
}
