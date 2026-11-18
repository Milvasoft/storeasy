using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// The price of a store product in one territory (region on Play), with the next scheduled change when there is one.
/// Amounts are the store's text (e.g. "49.99"), never computed by storeasy.
/// </summary>
public class ProductPrice
{
    /// <summary>
    /// Territory: App Store alpha-3 (TUR), Play alpha-2 (TR).
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Currency (ISO 4217).
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Price the customer pays.
    /// </summary>
    public string CustomerPrice { get; set; }

    /// <summary>
    /// What the developer receives (App Store).
    /// </summary>
    public string Proceeds { get; set; }

    /// <summary>
    /// Store's price point id (App Store).
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Store's id of the price record (App Store subscription price).
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Where the price comes from.
    /// </summary>
    public PriceSource Source { get; set; }

    /// <summary>
    /// First day of the price; null: since ever.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Customer price of the next scheduled change.
    /// </summary>
    public string PendingCustomerPrice { get; set; }

    /// <summary>
    /// Price point of the next scheduled change.
    /// </summary>
    public string PendingPricePointId { get; set; }

    /// <summary>
    /// First day of the next scheduled change.
    /// </summary>
    public DateOnly? PendingStartDate { get; set; }
}
