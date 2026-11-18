namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Testers of a Play testing track.
/// </summary>
public class PlayTestersDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Google Group addresses.
    /// </summary>
    public List<string> GoogleGroups { get; set; }
}
