namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Phased release of an App Store version.
/// </summary>
public class PhasedReleaseDto
{
    /// <summary>
    /// INACTIVE, ACTIVE, PAUSED or COMPLETE.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Day of the release (1-7).
    /// </summary>
    public int? CurrentDayNumber { get; set; }

    /// <summary>
    /// UTC start.
    /// </summary>
    public DateTime? StartDate { get; set; }
}
