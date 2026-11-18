namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// One desired listing value of a plan request.
/// </summary>
public class ListingChangeDto
{
    /// <summary>
    /// Locale; ignored for fields that are not localized.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Field name as the store names it (name, subtitle, description, keywords, title, shortDescription...).
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// New value; empty clears the field.
    /// </summary>
    public string Value { get; set; }
}
