namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Price schedule and availability of an App Store record.
/// </summary>
public class PricingDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Base territory.
    /// </summary>
    public string BaseTerritory { get; set; }

    /// <summary>
    /// Current base price point.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Price schedule.
    /// </summary>
    public List<PriceEntryDto> Schedule { get; set; }

    /// <summary>
    /// Territory availability.
    /// </summary>
    public List<TerritoryDto> Territories { get; set; }

    /// <summary>
    /// Whether new territories are added automatically.
    /// </summary>
    public bool? AvailableInNewTerritories { get; set; }
}
