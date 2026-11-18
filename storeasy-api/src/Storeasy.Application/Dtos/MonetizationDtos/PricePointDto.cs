namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A price point the store offers in one territory (tax included); storeasy never suggests an amount.
/// </summary>
public class PricePointDto
{
    /// <summary>
    /// The store's id of the price point.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Territory.
    /// </summary>
    public string Territory { get; set; }

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
}
