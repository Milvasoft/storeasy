using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Content;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// A change plan with its diff, remarks, store calls and state.
/// </summary>
public class ChangePlanDto : ChangePlanListDto
{
    /// <summary>
    /// App Store version the plan writes to.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Version string.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Text the approver must type, when a typed confirmation is needed.
    /// </summary>
    public string ConfirmText { get; set; }

    /// <summary>
    /// Field level diff.
    /// </summary>
    public List<PlanItem> Items { get; set; }

    /// <summary>
    /// Non blocking remarks.
    /// </summary>
    public List<PlanWarning> Warnings { get; set; }

    /// <summary>
    /// Store calls of the apply job.
    /// </summary>
    public List<PlanCall> Calls { get; set; }

    /// <summary>
    /// Check results of the uploaded files.
    /// </summary>
    public List<PlanFile> Files { get; set; }

    /// <summary>
    /// Fingerprint of the touched store values at planning time.
    /// </summary>
    public string BaseHash { get; set; }

    /// <summary>
    /// Rules catalog version used.
    /// </summary>
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Who decided.
    /// </summary>
    public string DecidedByUserName { get; set; }

    /// <summary>
    /// Reason of a rejection.
    /// </summary>
    public string DecisionNote { get; set; }

    /// <summary>
    /// UTC instant of the decision.
    /// </summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>
    /// UTC instant the store accepted every change.
    /// </summary>
    public DateTime? AppliedDate { get; set; }

    /// <summary>
    /// Error of a failed apply job.
    /// </summary>
    public string JobError { get; set; }

    /// <summary>
    /// Projection from a plan row (the apply job's error is filled after the read).
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static new Expression<Func<ChangePlan, ChangePlanDto>> Projection { get; } = p => new ChangePlanDto
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
        ConfirmText = p.ConfirmText,
        ItemCount = p.Items.Count,
        WarningCount = p.Warnings.Count,
        Status = p.Status,
        CreationDate = p.CreationDate,
        ExpiryDate = p.ExpiryDate,
        JobId = p.StoreJobId,
        VersionId = p.VersionId,
        VersionString = p.VersionString,
        Items = p.Items,
        Warnings = p.Warnings,
        Calls = p.Calls,
        Files = p.Files,
        BaseHash = p.BaseHash,
        CatalogVersion = p.CatalogVersion,
        DecidedByUserName = p.DecidedByUserName,
        DecisionNote = p.DecisionNote,
        DecisionDate = p.DecisionDate,
        AppliedDate = p.AppliedDate,
    };
}
