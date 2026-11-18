namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A subscription or In-App Purchase (App Store), a Play subscription or one-time product, as last synced.
/// </summary>
public class ProductDetailDto
{
    /// <summary>
    /// storeasy id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// What the product is.
    /// </summary>
    public StoreProductKind Kind { get; set; }

    /// <summary>
    /// Product id (read only after creation).
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Reference name.
    /// </summary>
    public string ReferenceName { get; set; }

    /// <summary>
    /// storeasy id of the subscription group.
    /// </summary>
    public long? GroupId { get; set; }

    /// <summary>
    /// Subscription period.
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
    /// State.
    /// </summary>
    public ProductState State { get; set; }

    /// <summary>
    /// The store's state text.
    /// </summary>
    public string StoreState { get; set; }

    /// <summary>
    /// Localizations.
    /// </summary>
    public List<ProductLocalization> Localizations { get; set; }

    /// <summary>
    /// Territories where the product is sold.
    /// </summary>
    public ProductAvailability Availability { get; set; }

    /// <summary>
    /// Number of territories where the product is sold.
    /// </summary>
    public int TerritoryCount { get; set; }

    /// <summary>
    /// Base territory price.
    /// </summary>
    public BasePriceDto BasePrice { get; set; }

    /// <summary>
    /// App Review screenshot.
    /// </summary>
    public ProductScreenshot ReviewScreenshot { get; set; }

    /// <summary>
    /// Note for App Review.
    /// </summary>
    public string ReviewNote { get; set; }

    /// <summary>
    /// Whether the product must be submitted together with an app version (the app's first subscription or In-App
    /// Purchase on the App Store).
    /// </summary>
    public bool SubmitWithVersion { get; set; }

    /// <summary>
    /// Reference name of the retention message assigned to the subscription.
    /// </summary>
    public string RetentionMessage { get; set; }

    /// <summary>
    /// Multiseat purchase status.
    /// </summary>
    public string MultiSeatStatus { get; set; }

    /// <summary>
    /// Play base plans or purchase options.
    /// </summary>
    public List<BasePlanDto> Plans { get; set; }

    /// <summary>
    /// Number of offers.
    /// </summary>
    public int OfferCount { get; set; }

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }
}
