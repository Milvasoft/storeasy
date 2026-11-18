namespace Storeasy.Domain.JsonModels;

/// <summary>
/// Territories where a store product is sold.
/// </summary>
public class ProductAvailability
{
    /// <summary>
    /// Store's id of the availability (App Store).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Available territories.
    /// </summary>
    public List<string> Territories { get; set; } = [];

    /// <summary>
    /// Whether territories the store adds later are included.
    /// </summary>
    public bool? AvailableInNewTerritories { get; set; }
}
