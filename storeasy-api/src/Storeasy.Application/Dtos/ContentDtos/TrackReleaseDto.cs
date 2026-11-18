namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A release on a Play track.
/// </summary>
public class TrackReleaseDto
{
    /// <summary>
    /// Release name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Version codes.
    /// </summary>
    public List<string> VersionCodes { get; set; }

    /// <summary>
    /// draft, inProgress, halted or completed.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Staged rollout fraction.
    /// </summary>
    public double? UserFraction { get; set; }

    /// <summary>
    /// Release notes.
    /// </summary>
    public List<ReleaseNote> ReleaseNotes { get; set; }

    /// <summary>
    /// Targeted countries; null for all.
    /// </summary>
    public List<string> Countries { get; set; }

    /// <summary>
    /// Whether the rest of the world is included.
    /// </summary>
    public bool? IncludeRestOfWorld { get; set; }
}
