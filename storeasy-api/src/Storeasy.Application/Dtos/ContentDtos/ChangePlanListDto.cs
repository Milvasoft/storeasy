using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Content;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A change plan in the approval queue.
/// </summary>
public class ChangePlanListDto
{
    /// <summary>
    /// Plan id.
    /// </summary>
    public long PlanId { get; set; }

    /// <summary>
    /// What the plan changes.
    /// </summary>
    public ChangePlanKind Kind { get; set; }

    /// <summary>
    /// One line description.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// App name.
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Where the plan was made.
    /// </summary>
    public ActionChannel Source { get; set; }

    /// <summary>
    /// Who made it.
    /// </summary>
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Permission needed to approve it.
    /// </summary>
    public OrgPermission RequiredPermission { get; set; }

    /// <summary>
    /// Whether the approval needs a typed confirmation.
    /// </summary>
    public bool ConfirmRequired { get; set; }

    /// <summary>
    /// Field level changes.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Remarks.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// State (Failed and Expired derived).
    /// </summary>
    public ChangePlanStatus Status { get; set; }

    /// <summary>
    /// UTC creation instant.
    /// </summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>
    /// UTC instant a pending plan expires.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Apply job.
    /// </summary>
    public long? JobId { get; set; }

    /// <summary>
    /// Projection from a plan row. <see cref="Status"/> is the stored state; <see cref="ChangePlanRules"/> derives
    /// Failed and Expired after the read.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<ChangePlan, ChangePlanListDto>> Projection { get; } = p => new ChangePlanListDto
    {
        PlanId = p.Id,
        Kind = p.Kind,
        Title = p.Title,
        AppId = p.AppId,
        AppName = p.StoreApp.App.Name,
        StoreAppId = p.StoreAppId,
        Store = p.Store,
        Source = p.Source,
        CreatedByUserName = p.CreatedByUserName,
        RequiredPermission = p.RequiredPermission,
        ConfirmRequired = p.ConfirmText != null,
        ItemCount = p.Items.Count,
        WarningCount = p.Warnings.Count,
        Status = p.Status,
        CreationDate = p.CreationDate,
        ExpiryDate = p.ExpiryDate,
        JobId = p.StoreJobId,
    };
}
