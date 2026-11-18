namespace Storeasy.Application.Dtos.AnswerDtos;

/// <summary>
/// Every saved answer document of an app.
/// </summary>
public class SavedAnswerSetDto
{
    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Documents that exist.
    /// </summary>
    public List<SavedAnswerSectionDto> Sections { get; set; }

    /// <summary>
    /// Whether a demo account password is sealed for App Review (never returned).
    /// </summary>
    public bool DemoPasswordSet { get; set; }
}
