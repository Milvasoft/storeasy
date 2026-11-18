namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// Title and subtitle of one slot in one locale.
/// </summary>
public class MockupCaptionDto
{
    /// <summary>
    /// Locale tag.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Slot position, from 1.
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// Title (a new line is a manual line break).
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Subtitle.
    /// </summary>
    public string Subtitle { get; set; }
}
