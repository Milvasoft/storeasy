namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// An App Store version with its release settings.
/// </summary>
public class StoreVersionDto
{
    /// <summary>
    /// Version id.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Platform.
    /// </summary>
    public string Platform { get; set; }

    /// <summary>
    /// Store state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// MANUAL, AFTER_APPROVAL or SCHEDULED.
    /// </summary>
    public string ReleaseType { get; set; }

    /// <summary>
    /// Earliest release instant.
    /// </summary>
    public DateTime? EarliestReleaseDate { get; set; }

    /// <summary>
    /// Attached build.
    /// </summary>
    public string BuildId { get; set; }

    /// <summary>
    /// Attached build number.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// Phased release, if any.
    /// </summary>
    public PhasedReleaseDto PhasedRelease { get; set; }
}
