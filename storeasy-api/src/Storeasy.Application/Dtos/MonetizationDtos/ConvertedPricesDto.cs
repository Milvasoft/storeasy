namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// The result of Play's price conversion: proposals the member reviews and adds to a plan; nothing is saved.
/// </summary>
public class ConvertedPricesDto
{
    /// <summary>
    /// Regional prices.
    /// </summary>
    public List<ConvertedPriceDto> Prices { get; set; } = [];

    /// <summary>
    /// Price for new regions in USD.
    /// </summary>
    public string OtherRegionsUsd { get; set; }

    /// <summary>
    /// Price for new regions in EUR.
    /// </summary>
    public string OtherRegionsEur { get; set; }

    /// <summary>
    /// Regions version the prices were made for (send it back with the plan).
    /// </summary>
    public string RegionsVersion { get; set; }

    /// <summary>
    /// UTC instant of the conversion.
    /// </summary>
    public DateTime ConvertedDate { get; set; }
}
