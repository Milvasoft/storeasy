namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Versions (App Store) or tracks (Play) of a store record.
/// </summary>
public class ReleaseOverviewDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// App Store versions, newest first.
    /// </summary>
    public List<StoreVersionDto> Versions { get; set; }

    /// <summary>
    /// Play tracks.
    /// </summary>
    public List<TrackDto> Tracks { get; set; }
}
