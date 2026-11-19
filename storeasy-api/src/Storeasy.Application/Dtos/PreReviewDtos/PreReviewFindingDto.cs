namespace Storeasy.Application.Dtos.PreReviewDtos;

/// <summary>
/// One problem an AI pre-review found.
/// </summary>
public class PreReviewFindingDto
{
    /// <summary>
    /// How serious it is.
    /// </summary>
    public PreReviewSeverity Severity { get; set; }

    /// <summary>
    /// Area of the submission.
    /// </summary>
    public PreReviewArea Area { get; set; }

    /// <summary>
    /// The store guideline it breaks.
    /// </summary>
    public string Guideline { get; set; }

    /// <summary>
    /// Short name of the problem.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Where it was seen (field, locale, screenshot).
    /// </summary>
    public string Evidence { get; set; }

    /// <summary>
    /// What to change.
    /// </summary>
    public string Fix { get; set; }
}
