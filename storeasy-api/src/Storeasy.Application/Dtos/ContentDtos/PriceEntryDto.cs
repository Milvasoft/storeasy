namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// An entry of the price schedule.
/// </summary>
public class PriceEntryDto
{
    /// <summary>
    /// Territory.
    /// </summary>
    public string Territory { get; set; }

    /// <summary>
    /// Price point.
    /// </summary>
    public string PricePointId { get; set; }

    /// <summary>
    /// Customer price.
    /// </summary>
    public string CustomerPrice { get; set; }

    /// <summary>
    /// First day.
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }
}
