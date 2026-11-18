namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// A deadline of the compliance timeline that applies (or may apply) to an app.
/// </summary>
public class ComplianceItemDto
{
    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Catalog rule.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Description in the language of the request.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Deadline (or the day the rule becomes required).
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Days until the due date (negative when passed).
    /// </summary>
    public int? DaysLeft { get; set; }

    /// <summary>
    /// Done, missing or unknown.
    /// </summary>
    public ComplianceStatus Status { get; set; }

    /// <summary>
    /// In force or upcoming.
    /// </summary>
    public RuleStatus RuleStatus { get; set; }

    /// <summary>
    /// Whether the rule applies.
    /// </summary>
    public RuleApplicability Applicability { get; set; }

    /// <summary>
    /// Matched conditions.
    /// </summary>
    public List<string> MatchReason { get; set; }

    /// <summary>
    /// Official source.
    /// </summary>
    public string SourceUrl { get; set; }

    /// <summary>
    /// Day the source was checked.
    /// </summary>
    public DateOnly? CheckedDate { get; set; }

    /// <summary>
    /// Source conflict and the reading the catalog uses.
    /// </summary>
    public string Conflict { get; set; }

    /// <summary>
    /// Assisted step that records it, if one.
    /// </summary>
    public int? AssistedStepId { get; set; }
}
