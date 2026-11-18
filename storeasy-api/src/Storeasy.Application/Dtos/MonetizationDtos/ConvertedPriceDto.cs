namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A regional price Play's conversion calculated (tax included).
/// </summary>
public class ConvertedPriceDto
{
    /// <summary>
    /// Region (alpha-2).
    /// </summary>
    public string RegionCode { get; set; }

    /// <summary>
    /// Currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Amount.
    /// </summary>
    public string Amount { get; set; }
}
