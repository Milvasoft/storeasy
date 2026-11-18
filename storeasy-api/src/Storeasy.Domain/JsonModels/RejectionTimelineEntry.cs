using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One line of the timeline of a rejection case.
/// </summary>
public class RejectionTimelineEntry
{
    /// <summary>
    /// UTC instant.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Machine name of what happened (<c>opened</c>, <c>text</c>, <c>checklist</c>, <c>draft</c>, <c>sent</c>, <c>closed</c>).
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Short note (who, what).
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Where it came from.
    /// </summary>
    public ActionChannel Channel { get; set; }
}
