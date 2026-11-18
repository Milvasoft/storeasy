namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// A render warning or failure, or a caption that overflows. Codes are listed in storeasy-api/docs/studio.md.
/// </summary>
public class MockupWarningDto
{
    /// <summary>
    /// Machine code (e.g. <c>CaptionOverflow</c>, <c>AssetTooSmall</c>).
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// English detail.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Screenshot class the warning is about.
    /// </summary>
    public string ClassKey { get; set; }

    /// <summary>
    /// Locale the warning is about.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Slot the warning is about.
    /// </summary>
    public int? SlotOrder { get; set; }

    /// <summary>
    /// <c>title</c> or <c>subtitle</c> for caption warnings.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Lines the caption needs.
    /// </summary>
    public int? Lines { get; set; }

    /// <summary>
    /// Lines the caption style allows.
    /// </summary>
    public int? MaxLines { get; set; }

    /// <summary>
    /// Maps a renderer issue.
    /// </summary>
    /// <param name="issue"></param>
    /// <param name="classKey"></param>
    /// <param name="locale"></param>
    /// <param name="slotOrder"></param>
    /// <returns></returns>
    public static MockupWarningDto From(MockupRenderIssue issue, string classKey, string locale, int? slotOrder) => new()
    {
        Code = issue.Code,
        Message = issue.Message,
        ClassKey = classKey,
        Locale = locale,
        SlotOrder = slotOrder,
        Field = issue.Field,
        Lines = issue.Lines,
        MaxLines = issue.MaxLines,
    };
}
