namespace Storeasy.Domain.JsonModels;

/// <summary>
/// An app in a store's search results for a keyword.
/// </summary>
public class AsoCompetitor
{
    /// <summary>
    /// Position in the results, 1 based.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Store id (App Store track id, Play package name).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// App name, when the store returned it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Developer, when known.
    /// </summary>
    public string Developer { get; set; }

    /// <summary>
    /// Average rating, 0 to 5, when known.
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
}
