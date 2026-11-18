namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A locale of a listing with its fill state.
/// </summary>
public class ListingLocaleDto
{
    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Whether it is the primary (default) locale.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Required fields with a value.
    /// </summary>
    public int FilledCount { get; set; }

    /// <summary>
    /// Required fields.
    /// </summary>
    public int RequiredCount { get; set; }
}
