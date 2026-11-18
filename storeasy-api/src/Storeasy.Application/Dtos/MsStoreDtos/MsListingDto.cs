namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// Listing texts of one language. As input, a null field (or list) leaves the store's value as it is.
/// </summary>
public class MsListingDto
{
    /// <summary>
    /// Language (en-us, tr-tr).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Title (MSIX: base listing title; read only for MSI/EXE).
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Description, up to 10000 characters.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Short description, up to 1000 characters.
    /// </summary>
    public string ShortDescription { get; set; }

    /// <summary>
    /// What's new in this version, up to 1500 characters.
    /// </summary>
    public string WhatsNew { get; set; }

    /// <summary>
    /// Product features, up to 20 of 200 characters.
    /// </summary>
    public List<string> Features { get; set; }

    /// <summary>
    /// Search terms (MSIX keywords), up to 7 of 30 characters.
    /// </summary>
    public List<string> SearchTerms { get; set; }
}
