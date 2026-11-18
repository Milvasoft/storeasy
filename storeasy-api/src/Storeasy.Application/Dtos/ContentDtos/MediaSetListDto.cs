namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// The media classes of one locale of a store record, evaluated against the rules catalog.
/// </summary>
public class MediaSetListDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Screenshots (and graphics) or previews.
    /// </summary>
    public MediaKind Kind { get; set; }

    /// <summary>
    /// Locale.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// App Store version.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Catalog version.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Classes.
    /// </summary>
    public List<MediaClassDto> Classes { get; set; }
}
