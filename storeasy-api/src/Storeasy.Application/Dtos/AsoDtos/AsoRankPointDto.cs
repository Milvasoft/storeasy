namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// One day of a rank series.
/// </summary>
public class AsoRankPointDto
{
    /// <summary>
    /// UTC day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Whether the rank was checked that day.
    /// </summary>
    public bool Checked { get; set; }

    /// <summary>
    /// Position, 1 based; null when not checked or not in the results read.
    /// </summary>
    public int? Rank { get; set; }
}
