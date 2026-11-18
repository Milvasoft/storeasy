namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A Play track with its releases and testers.
/// </summary>
public class TrackDto
{
    /// <summary>
    /// Track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Releases.
    /// </summary>
    public List<TrackReleaseDto> Releases { get; set; }

    /// <summary>
    /// Tester Google Groups.
    /// </summary>
    public List<string> GoogleGroups { get; set; }
}
