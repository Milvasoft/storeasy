namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// One requested change of a monetization plan. Which fields are read depends on <see cref="Kind"/>; products are named
/// by <see cref="ProductId"/> (or <see cref="SubscriptionId"/>, the storeasy id). The same object, completed with the
/// store's ids by the planner, is what the apply job writes.
/// </summary>
public class MonetizationChangeDto
{
    /// <summary>
    /// What the change does.
    /// </summary>
    public MonetizationChangeKind Kind { get; set; }

    /// <summary>
    /// storeasy id of the product (subscription or In-App Purchase); the alternative to <see cref="ProductId"/>.
    /// </summary>
    public long? SubscriptionId { get; set; }

    /// <summary>
    /// Product id the app uses.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Kind of a new In-App Purchase (Consumable, NonConsumable, NonRenewingSubscription).
    /// </summary>
    public StoreProductKind? ProductKind { get; set; }

    /// <summary>
    /// storeasy id of the subscription group.
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// Reference name of the group (a group created in the same plan is named by it).
    /// </summary>
    public string GroupReferenceName { get; set; }

    /// <summary>
    /// Reference name of a product or offer.
    /// </summary>
    public string ReferenceName { get; set; }

    /// <summary>
    /// Locale of a localization change.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Display name (App Store) or title (Play) of a localization.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description of a localization.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Custom app name of a group localization.
    /// </summary>
    public string CustomAppName { get; set; }

    /// <summary>
    /// Benefits of a Play subscription listing.
    /// </summary>
    public List<string> Benefits { get; set; }

    /// <summary>
    /// Localizations of a new group or Play product.
    /// </summary>
    public List<LocalizationInputDto> Localizations { get; set; }

    /// <summary>
    /// Subscription period (ONE_MONTH) or offer duration (ONE_WEEK; ISO 8601 on Play).
    /// </summary>
    public string Duration { get; set; }

    /// <summary>
    /// Level in the group.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Family Sharing (cannot be turned off once on).
    /// </summary>
    public bool? FamilySharable { get; set; }

    /// <summary>
    /// Note for App Review.
    /// </summary>
    public string ReviewNote { get; set; }

    /// <summary>
    /// Multiseat purchase status (ENABLED, DISABLED).
    /// </summary>
    public string MultiSeatStatus { get; set; }

    /// <summary>
    /// Territory of a price change (base territory of <see cref="MonetizationChangeKind.BasePrice"/>).
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Price point of the territory (App Store; storeasy never makes up an amount).
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// First day of a price or offer; null: right away.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Last day of an offer.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// For a price change of a product on sale: true keeps the current price for existing subscribers, false applies the
    /// new price to them too (needs the typed confirmation). Required in that case.
    /// </summary>
    public bool? PreserveCurrentPrice { get; set; }

    /// <summary>
    /// Base price: also set the store's equalized price in every other territory, except territories set by hand.
    /// </summary>
    public bool Equalize { get; set; } = true;

    /// <summary>
    /// Prices per territory (offers, Play regional prices, resolved equalization).
    /// </summary>
    public List<PriceInputDto> Prices { get; set; }

    /// <summary>
    /// Territories (availability, offer territories).
    /// </summary>
    public List<string> Territories { get; set; }

    /// <summary>
    /// Whether territories added later are included.
    /// </summary>
    public bool? AvailableInNewTerritories { get; set; }

    /// <summary>
    /// Offer payment mode.
    /// </summary>
    public OfferMode? Mode { get; set; }

    /// <summary>
    /// Number of offer periods.
    /// </summary>
    public int? Periods { get; set; }

    /// <summary>
    /// Offer identifier: promotional offer code, win-back offer id, custom offer code, Play offer id.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Offer code eligibility (NEW, EXISTING, EXPIRED; Play: NewCustomers, Upgrade, DeveloperDetermined).
    /// </summary>
    public List<string> Eligibility { get; set; }

    /// <summary>
    /// Offer code: whether it stacks with introductory offers (STACK_WITH_INTRO_OFFERS, REPLACE_INTRO_OFFERS).
    /// </summary>
    public string OfferEligibility { get; set; }

    /// <summary>
    /// Offer code: whether the subscription renews after the offer.
    /// </summary>
    public bool? AutoRenew { get; set; }

    /// <summary>
    /// Offer code: number of redemptions of the custom code.
    /// </summary>
    public int? CustomCodeCount { get; set; }

    /// <summary>
    /// Offer code: number of one-time use codes to create (the codes are downloaded from App Store Connect).
    /// </summary>
    public int? OneTimeCodeCount { get; set; }

    /// <summary>
    /// Offer code: last day the codes can be redeemed.
    /// </summary>
    public DateOnly? CodeExpiryDate { get; set; }

    /// <summary>
    /// Activate (true) or deactivate (false) a base plan, offer, purchase option or offer code.
    /// </summary>
    public bool? Active { get; set; }

    /// <summary>
    /// Win-back: months the customer was a paid subscriber.
    /// </summary>
    public int? PaidMonths { get; set; }

    /// <summary>
    /// Win-back: least months since the last subscription.
    /// </summary>
    public int? SinceMinMonths { get; set; }

    /// <summary>
    /// Win-back: most months since the last subscription.
    /// </summary>
    public int? SinceMaxMonths { get; set; }

    /// <summary>
    /// Win-back: months between two redemptions.
    /// </summary>
    public int? WaitMonths { get; set; }

    /// <summary>
    /// Win-back priority (HIGH, NORMAL).
    /// </summary>
    public string Priority { get; set; }

    /// <summary>
    /// Uploaded asset of a review screenshot.
    /// </summary>
    public long? AssetId { get; set; }

    /// <summary>
    /// Play base plan id.
    /// </summary>
    public string BasePlanId { get; set; }

    /// <summary>
    /// Play base plan: auto renewing (true) or prepaid (false).
    /// </summary>
    public bool? AutoRenewing { get; set; }

    /// <summary>
    /// Play offer phases.
    /// </summary>
    public List<OfferPhaseInputDto> Phases { get; set; }

    /// <summary>
    /// Play price for new regions in USD.
    /// </summary>
    public string OtherRegionsUsd { get; set; }

    /// <summary>
    /// Play price for new regions in EUR.
    /// </summary>
    public string OtherRegionsEur { get; set; }

    /// <summary>
    /// Play one-time product purchase option id.
    /// </summary>
    public string PurchaseOptionId { get; set; }

    /// <summary>
    /// App Store Server Notifications production address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// App Store Server Notifications sandbox address.
    /// </summary>
    public string SandboxUrl { get; set; }

    /// <summary>
    /// Notification version (V1, V2).
    /// </summary>
    public string NotificationVersion { get; set; }

    /// <summary>
    /// Sandbox tester id.
    /// </summary>
    public string TesterId { get; set; }

    /// <summary>
    /// Sandbox tester: interrupt purchases.
    /// </summary>
    public bool? InterruptPurchases { get; set; }

    /// <summary>
    /// Sandbox tester: renewal rate (MONTHLY_RENEWAL_EVERY_ONE_HOUR...).
    /// </summary>
    public string RenewalRate { get; set; }

    /// <summary>
    /// Sandbox tester: clear the purchase history.
    /// </summary>
    public bool? ClearPurchaseHistory { get; set; }

    /// <summary>
    /// Store's id of the product, set by the planner.
    /// </summary>
    public string ProductExternalId { get; set; }

    /// <summary>
    /// Store's id of the group, set by the planner.
    /// </summary>
    public string GroupExternalId { get; set; }

    /// <summary>
    /// Object key of the asset, set by the planner.
    /// </summary>
    public string ObjectKey { get; set; }

    /// <summary>
    /// File name of the asset, set by the planner.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Lower case hex MD5 of the asset, set by the planner.
    /// </summary>
    public string Md5 { get; set; }

    /// <summary>
    /// Size of the asset in bytes, set by the planner.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Play regions version the prices were made for (from the price conversion); the default when null.
    /// </summary>
    public string RegionsVersion { get; set; }

    /// <summary>
    /// A shallow copy (the planner completes the copy, never the request).
    /// </summary>
    /// <returns></returns>
    public MonetizationChangeDto Copy() => (MonetizationChangeDto)MemberwiseClone();
}
