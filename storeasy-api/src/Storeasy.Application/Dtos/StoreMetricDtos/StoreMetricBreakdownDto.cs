namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Totals of one group of a breakdown: a traffic source, a country or a store (exactly one of the keys is set).
/// </summary>
public class StoreMetricBreakdownDto
{
    /// <summary>
    /// Traffic source of the group.
    /// </summary>
    public TrafficSource? Source { get; set; }

    /// <summary>
    /// Country code of the group (<c>ZZ</c> when the store did not name it).
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Store of the group.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Totals.
    /// </summary>
    public StoreMetricValuesDto Totals { get; set; }
}
