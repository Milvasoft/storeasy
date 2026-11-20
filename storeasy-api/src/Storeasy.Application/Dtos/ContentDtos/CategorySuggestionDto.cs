namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// An AI suggestion for the App Store categories. Nothing was saved: the member reviews it and plans it with categories (PUT).
/// </summary>
public class CategorySuggestionDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Current primary category id.
    /// </summary>
    public string CurrentPrimary { get; set; }

    /// <summary>
    /// Current secondary category id.
    /// </summary>
    public string CurrentSecondary { get; set; }

    /// <summary>
    /// Suggested primary category id (one of the store's).
    /// </summary>
    public string Primary { get; set; }

    /// <summary>
    /// Suggested secondary category id, or null for none.
    /// </summary>
    public string Secondary { get; set; }

    /// <summary>
    /// Why, in the listing's primary language (from the model).
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Model that wrote it.
    /// </summary>
    public string Model { get; set; }
}
