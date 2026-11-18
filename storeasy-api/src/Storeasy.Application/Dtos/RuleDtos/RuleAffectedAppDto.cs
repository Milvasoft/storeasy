namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// An app of the organization matched against a rule.
/// </summary>
public class RuleAffectedAppDto
{
    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Whether the rule applies.
    /// </summary>
    public RuleApplicability Applicability { get; set; }

    /// <summary>
    /// Matched conditions.
    /// </summary>
    public List<string> Reasons { get; set; }

    /// <summary>
    /// Conditions storeasy could not decide (app data missing).
    /// </summary>
    public List<string> UnknownKeys { get; set; }
}
