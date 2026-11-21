namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One window of a before and after comparison.
/// </summary>
public class AsoImpactWindowDto
{
    /// <summary>
    /// First day.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// Last day.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// Days of the window with store page metrics.
    /// </summary>
    public int DaysWithData { get; set; }
}
