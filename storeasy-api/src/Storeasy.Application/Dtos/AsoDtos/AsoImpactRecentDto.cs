namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// The impact of the organization's recent changes.
/// </summary>
public class AsoImpactRecentDto
{
    /// <summary>
    /// First effect day listed.
    /// </summary>
    public DateOnly From { get; set; }

    /// <summary>
    /// Last effect day listed.
    /// </summary>
    public DateOnly To { get; set; }

    /// <summary>
    /// Window length used.
    /// </summary>
    public int WindowDays { get; set; }

    /// <summary>
    /// Changes, newest first.
    /// </summary>
    public List<AsoImpactSummaryDto> Items { get; set; } = [];
}
