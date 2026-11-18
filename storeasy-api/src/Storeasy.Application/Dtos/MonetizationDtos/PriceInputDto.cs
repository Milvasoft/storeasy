namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A price of one territory in a change: an App Store price point, or a Play amount in the region's currency.
/// </summary>
public class PriceInputDto
{
    /// <summary>
    /// Territory (App Store alpha-3) or region (Play alpha-2).
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// App Store price point.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Currency (Play).
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Amount with a period as decimal separator (Play).
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Customer price of the price point, filled by the planner for the diff.
    /// </summary>
    public string CustomerPrice { get; set; }
}
