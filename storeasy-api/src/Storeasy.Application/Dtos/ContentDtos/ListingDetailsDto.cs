namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// Google Play app details (edits.details).
/// </summary>
public class ListingDetailsDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Contact e-mail (required).
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Contact phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Contact website.
    /// </summary>
    public string ContactWebsite { get; set; }

    /// <summary>
    /// Default language.
    /// </summary>
    public string DefaultLanguage { get; set; }
}
