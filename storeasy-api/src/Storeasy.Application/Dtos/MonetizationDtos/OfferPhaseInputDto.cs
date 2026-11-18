namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A phase of a Play subscription offer.
/// </summary>
public class OfferPhaseInputDto
{
    /// <summary>
    /// Duration of one recurrence (ISO 8601, e.g. P1W).
    /// </summary>
    public string Duration { get; set; }

    /// <summary>
    /// Number of recurrences.
    /// </summary>
    public int Recurrence { get; set; } = 1;

    /// <summary>
    /// Free phase (trial); otherwise <see cref="Prices"/> are the absolute prices per region.
    /// </summary>
    public bool Free { get; set; }

    /// <summary>
    /// Regional prices of a paid phase.
    /// </summary>
    public List<PriceInputDto> Prices { get; set; }
}
