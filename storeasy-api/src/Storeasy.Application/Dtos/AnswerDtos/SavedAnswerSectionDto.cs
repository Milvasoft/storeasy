using System.Text.Json;

namespace Storeasy.Application.Dtos.AnswerDtos;

/// <summary>
/// One saved answer document of an app.
/// </summary>
public class SavedAnswerSectionDto
{
    /// <summary>
    /// Which form the answers are for.
    /// </summary>
    public SavedAnswerKind Key { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// Who changed it last.
    /// </summary>
    public string UpdatedByUserName { get; set; }

    /// <summary>
    /// The answers (typed document of the kind). Secrets are never included.
    /// </summary>
    public JsonElement Answers { get; set; }
}
