namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// A catalog rule in a list, with its title in the language of the request.
/// </summary>
public class RuleListDto
{
    /// <summary>
    /// Rule id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Category (screenshots, age-rating, target-api...).
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Requirement level (required, required-from, recommended, optional).
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// State today.
    /// </summary>
    public RuleStatus Status { get; set; }

    /// <summary>
    /// Day the rule started or starts to apply.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>
    /// Day by which affected apps must comply.
    /// </summary>
    public DateOnly? Deadline { get; set; }

    /// <summary>
    /// The date that matters for the timeline.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// automatic, assisted or info.
    /// </summary>
    public string CheckKind { get; set; }

    /// <summary>
    /// Whether every fact was confirmed against the sources.
    /// </summary>
    public bool Verified { get; set; }

    /// <summary>
    /// First official source.
    /// </summary>
    public string SourceUrl { get; set; }

    /// <summary>
    /// Day the first source was checked.
    /// </summary>
    public DateOnly? CheckedDate { get; set; }

    /// <summary>
    /// Apps of the organization the rule applies to (or may apply to).
    /// </summary>
    public int AffectedAppCount { get; set; }
}
