namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// An app in the search results of a keyword.
/// </summary>
public class AsoCompetitorDto
{
    /// <summary>
    /// Position, 1 based.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Store id (App Store track id, Play package name).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Name, when known.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Developer, when known.
    /// </summary>
    public string Developer { get; set; }

    /// <summary>
    /// Average rating 0 to 5, when known.
    /// </summary>
    public double? Rating { get; set; }

    /// <summary>
    /// Number of ratings, when known.
    /// </summary>
    public long? RatingCount { get; set; }

    /// <summary>
    /// Icon URL, when known.
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// Whether it is the app itself.
    /// </summary>
    public bool IsOwnApp { get; set; }
}
