namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// An AI suggestion for one listing field. Nothing was saved: the member edits it and plans it with listings/plan.
/// </summary>
public class ListingSuggestionDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Language.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Listing field key.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// The field's value in the store when the suggestion was made.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// Suggested value, within the field's limits.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Why, in the field's language (from the model).
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Whether the model's text had to be shortened to fit the limit.
    /// </summary>
    public bool Shortened { get; set; }

    /// <summary>
    /// Character limit of the field, when the catalog has one.
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Byte limit of the field, when the catalog has one.
    /// </summary>
    public int? MaxBytes { get; set; }

    /// <summary>
    /// Model that wrote it.
    /// </summary>
    public string Model { get; set; }
}
