namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Listing of an add-on in one language.
/// </summary>
public class MsAddonListingDto
{
    /// <summary>
    /// Language.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Title, up to 100 characters.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Description, up to 200 characters.
    /// </summary>
    public string Description { get; set; }
}
