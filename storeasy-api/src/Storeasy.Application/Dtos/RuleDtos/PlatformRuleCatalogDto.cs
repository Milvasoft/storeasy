namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// The loaded rules catalog as a platform manager reads it, without any organization (no app counts).
/// </summary>
public class PlatformRuleCatalogDto
{
    /// <summary>
    /// Version and source of the catalog.
    /// </summary>
    public RuleCatalogInfoDto Catalog { get; set; }

    /// <summary>
    /// Active rules (deprecated ones left out), by due date.
    /// </summary>
    public List<RuleListDto> Rules { get; set; }
}
