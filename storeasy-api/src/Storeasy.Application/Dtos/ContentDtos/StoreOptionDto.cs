namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A choice the store offers.
/// </summary>
public class StoreOptionDto
{
    /// <summary>
    /// The store's id.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Display text.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Additional value (price point: customer price).
    /// </summary>
    public string Extra { get; set; }
}
