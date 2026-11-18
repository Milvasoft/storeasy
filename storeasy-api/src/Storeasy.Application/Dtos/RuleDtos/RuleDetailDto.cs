using Storeasy.Domain.Rules;

namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// A catalog rule with everything the catalog says about it and the organization's apps it applies to.
/// </summary>
public class RuleDetailDto : RuleListDto
{
    /// <summary>
    /// Description in the language of the request.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Wording of the requirement condition.
    /// </summary>
    public string Condition { get; set; }

    /// <summary>
    /// Rules the store derives a missing asset from.
    /// </summary>
    public List<string> FallbackChain { get; set; }

    /// <summary>
    /// Rules that also meet the requirement.
    /// </summary>
    public List<string> SatisfiedBy { get; set; }

    /// <summary>
    /// Conditions on the app.
    /// </summary>
    public RuleConditions AppliesWhen { get; set; }

    /// <summary>
    /// Machine checkable values.
    /// </summary>
    public RuleParams Params { get; set; }

    /// <summary>
    /// Official sources.
    /// </summary>
    public List<RuleSource> Sources { get; set; }

    /// <summary>
    /// Disagreements between sources and the reading the catalog uses.
    /// </summary>
    public List<RuleConflict> Conflicts { get; set; }

    /// <summary>
    /// Notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Source wording of an approximate date.
    /// </summary>
    public string DateNote { get; set; }

    /// <summary>
    /// Catalog version.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// The organization's apps matched against the rule.
    /// </summary>
    public List<RuleAffectedAppDto> AffectedApps { get; set; }
}
