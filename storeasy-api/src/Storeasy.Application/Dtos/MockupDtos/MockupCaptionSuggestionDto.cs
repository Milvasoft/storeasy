namespace Storeasy.Application.Dtos.MockupDtos;

/// <summary>
/// Suggested captions for one locale. Nothing is saved: the client puts accepted suggestions into the grid as unsaved
/// cells and saves them with the caption endpoint.
/// </summary>
public class MockupCaptionSuggestionDto
{
    /// <summary>
    /// Locale the suggestions are written in.
    /// </summary>
    public string TargetLocale { get; set; }

    /// <summary>
    /// Suggestions.
    /// </summary>
    public List<MockupCaptionSuggestionItemDto> Items { get; set; }
}

/// <summary>
/// One suggested caption field.
/// </summary>
public class MockupCaptionSuggestionItemDto
{
    /// <summary>
    /// Slot position.
    /// </summary>
    public int SlotOrder { get; set; }

    /// <summary>
    /// <c>title</c> or <c>subtitle</c>.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Current text in the target locale.
    /// </summary>
    public string Before { get; set; }

    /// <summary>
    /// Suggested text.
    /// </summary>
    public string After { get; set; }

    /// <summary>
    /// Short reason.
    /// </summary>
    public string Reason { get; set; }
}
