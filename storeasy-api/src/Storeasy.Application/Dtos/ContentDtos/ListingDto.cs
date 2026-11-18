namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// The listing texts of a store record, read live from the store, with the catalog limits.
/// </summary>
public class ListingDto
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
    /// App Store version the version fields belong to.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Store state of the version.
    /// </summary>
    public string VersionState { get; set; }

    /// <summary>
    /// Whether the version fields can be changed.
    /// </summary>
    public bool VersionEditable { get; set; }

    /// <summary>
    /// Catalog version of the limits.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Locales of the listing.
    /// </summary>
    public List<ListingLocaleDto> Locales { get; set; }

    /// <summary>
    /// Fields of the requested locale (every locale when none was asked).
    /// </summary>
    public List<ListingFieldDto> Fields { get; set; }
}
