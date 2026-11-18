namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// Version of the loaded rules catalog.
/// </summary>
public class RuleCatalogInfoDto
{
    /// <summary>
    /// Catalog version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// UTC instant the catalog data was generated.
    /// </summary>
    public DateTime PublishedDate { get; set; }

    /// <summary>
    /// Rules in the catalog (deprecated ones included).
    /// </summary>
    public int RuleCount { get; set; }

    /// <summary>
    /// Where the catalog was loaded from (folder or embedded).
    /// </summary>
    public string Source { get; set; }
}
