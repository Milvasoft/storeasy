namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One day of a window, for the paired chart (day <see cref="Offset"/> of the before window next to the same offset of
/// the after window).
/// </summary>
public class AsoImpactDayDto
{
    /// <summary>
    /// Day of the window, 0 based.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Impressions; null when not reported.
    /// </summary>
    public long? Impressions { get; set; }

    /// <summary>
    /// Page views; null when not reported.
    /// </summary>
    public long? PageViews { get; set; }

    /// <summary>
    /// Downloads; null when not reported.
    /// </summary>
    public long? Downloads { get; set; }

    /// <summary>
    /// Conversion rate; null when not reported.
    /// </summary>
    public double? ConversionRate { get; set; }
}
