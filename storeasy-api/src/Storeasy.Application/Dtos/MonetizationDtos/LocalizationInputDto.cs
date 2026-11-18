namespace Storeasy.Application.Dtos.MonetizationDtos;

/// <summary>
/// A localization in a change (new group, Play listing).
/// </summary>
public class LocalizationInputDto
{
    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Display name or title.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Custom app name of a group localization.
    /// </summary>
    public string CustomAppName { get; set; }

    /// <summary>
    /// Benefits of a Play subscription listing.
    /// </summary>
    public List<string> Benefits { get; set; }
}
