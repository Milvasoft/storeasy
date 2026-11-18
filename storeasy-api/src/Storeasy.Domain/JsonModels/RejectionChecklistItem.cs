using Storeasy.Domain.Enums;

namespace Storeasy.Domain.JsonModels;

/// <summary>
/// One fix item of a rejection case.
/// </summary>
public class RejectionChecklistItem
{
    /// <summary>
    /// Stable id of the item inside its case.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Guideline the item answers; null for an item added by hand without one.
    /// </summary>
    public string GuidelineCode { get; set; }

    /// <summary>
    /// What to do.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Where in storeasy the fix is made (a web app route), if known.
    /// </summary>
    public string FixRoute { get; set; }

    /// <summary>
    /// Member responsible for the item.
    /// </summary>
    public int? AssigneeUserId { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ChecklistItemStatus Status { get; set; } = ChecklistItemStatus.Todo;

    /// <summary>
    /// UTC instant the item was marked done.
    /// </summary>
    public DateTime? DoneDate { get; set; }
}
