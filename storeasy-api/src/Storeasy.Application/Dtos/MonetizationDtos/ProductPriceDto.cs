namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A row of the territory price table of a product.
/// </summary>
public class ProductPriceDto
{
    /// <summary>
    /// Territory code.
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// English name of the territory.
    /// </summary>
    public string TerritoryName { get; set; }

    /// <summary>
    /// Currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Price the customer pays.
    /// </summary>
    public string CustomerPrice { get; set; }

    /// <summary>
    /// What the developer receives.
    /// </summary>
    public string Proceeds { get; set; }

    /// <summary>
    /// App Store price point.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Where the price comes from.
    /// </summary>
    public PriceSource Source { get; set; }

    /// <summary>
    /// Whether this is the base territory.
    /// </summary>
    public bool IsBase { get; set; }

    /// <summary>
    /// First day of the price.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Whether a future change is scheduled.
    /// </summary>
    public bool HasPendingChange { get; set; }

    /// <summary>
    /// Customer price of the scheduled change.
    /// </summary>
    public string PendingCustomerPrice { get; set; }

    /// <summary>
    /// First day of the scheduled change.
    /// </summary>
    public DateOnly? PendingStartDate { get; set; }
}
