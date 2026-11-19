using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One problem an AI pre-review found in a submission.
/// </summary>
public class PreReviewFinding
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
    /// The store guideline it breaks (e.g. <c>App Store Review Guideline 2.3.7</c>, a Google Play policy name, a Microsoft
    /// Store policy number).
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
