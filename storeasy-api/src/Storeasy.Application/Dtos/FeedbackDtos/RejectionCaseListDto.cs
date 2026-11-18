using Storeasy.Domain.Feedback;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// A rejection case in the list.
/// </summary>
public class RejectionCaseListDto
{
    /// <summary>Case id.</summary>
    public int Id { get; set; }

    /// <summary>storeasy app.</summary>
    public int AppId { get; set; }

    /// <summary>Store.</summary>
    public StoreKind Store { get; set; }

    /// <summary>Store record.</summary>
    public int StoreAppId { get; set; }

    /// <summary>Version.</summary>
    public string VersionString { get; set; }

    /// <summary>Build number.</summary>
    public string BuildNumber { get; set; }

    /// <summary>What was rejected.</summary>
    public RejectionKind Kind { get; set; }

    /// <summary>State.</summary>
    public RejectionState State { get; set; }

    /// <summary>Cited guidelines.</summary>
    public List<RejectionGuideline> Guidelines { get; set; }

    /// <summary>How it was opened.</summary>
    public RejectionSource OpenedVia { get; set; }

    /// <summary>UTC instant opened.</summary>
    public DateTime OpenedDate { get; set; }

    /// <summary>UTC instant closed.</summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>Checklist items done.</summary>
    public int ChecklistDone { get; set; }

    /// <summary>Checklist items.</summary>
    public int ChecklistTotal { get; set; }

    /// <summary>
    /// Projection from the entity (the checklist counts are filled in memory).
    /// </summary>
    [JsonIgnore]
    public static Expression<Func<RejectionCase, RejectionCaseListDto>> Projection { get; } = c => new RejectionCaseListDto
    {
        Id = c.Id,
        AppId = c.AppId,
        Store = c.Store,
        StoreAppId = c.StoreAppId,
        VersionString = c.VersionString,
        BuildNumber = c.BuildNumber,
        Kind = c.Kind,
        State = c.State,
        Guidelines = c.Guidelines,
        OpenedVia = c.OpenedVia,
        OpenedDate = c.OpenedDate,
        ClosedDate = c.ClosedDate,
        ChecklistItems = c.Checklist,
    };

    /// <summary>
    /// The checklist as read (the counts are computed from it).
    /// </summary>
    [JsonIgnore]
    public List<RejectionChecklistItem> ChecklistItems { get; set; }

    /// <summary>
    /// Fills the counts from <see cref="ChecklistItems"/>.
    /// </summary>
    public RejectionCaseListDto WithCounts()
    {
        ChecklistTotal = ChecklistItems?.Count ?? 0;
        ChecklistDone = ChecklistItems?.Count(i => i.Status == ChecklistItemStatus.Done) ?? 0;

        return this;
    }
}
