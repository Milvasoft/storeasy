namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// One answer of the age rating declaration.
/// </summary>
public class AgeRatingAnswerDto
{
    /// <summary>
    /// Attribute name.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Value as raw JSON ("NONE", true...); null when unanswered.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Whether the question is one of the new descriptors.
    /// </summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Whether the API cannot write it (answered as an assisted step).
    /// </summary>
    public bool Assisted { get; set; }

    /// <summary>
    /// Deadline of a new question.
    /// </summary>
    public DateOnly? DueDate { get; set; }
}
