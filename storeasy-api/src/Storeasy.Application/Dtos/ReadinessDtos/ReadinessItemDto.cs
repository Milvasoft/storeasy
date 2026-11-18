namespace Storeasy.Application.Dtos.ReadinessDtos;

/// <summary>
/// One readiness check.
/// </summary>
public class ReadinessItemDto
{
    /// <summary>
    /// Stable key of the check (texts.en-US, screenshots.APP_IPAD_PRO_3GEN_129, build...).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Group (Texts, Media, Build, Review, AgeRating, Categories, Pricing, Answers, Compliance, TargetApi...).
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Outcome.
    /// </summary>
    public ReadinessStatus Status { get; set; }

    /// <summary>
    /// Catalog rule behind the check, if one.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Title in the language of the request.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// What is wrong, in the language of the request.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Locale, for per locale checks.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Where the web app fixes it.
    /// </summary>
    public string FixRoute { get; set; }

    /// <summary>
    /// Deadline, for timeline checks.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Media classes, for media checks.
    /// </summary>
    public List<ReadinessSlotDto> Slots { get; set; }
}
