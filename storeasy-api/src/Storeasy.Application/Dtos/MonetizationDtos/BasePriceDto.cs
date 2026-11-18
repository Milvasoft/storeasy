namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// The price of a product in its base territory (App Store) or default region (Play), as the store has it.
/// </summary>
public class BasePriceDto
{
    /// <summary>
    /// Territory or region.
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Amount (store text).
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Where the price comes from.
    /// </summary>
    public PriceSource Source { get; set; }
}
