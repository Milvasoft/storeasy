using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Content;

/// <summary>
/// A change plan: every write to a store goes through one (docs/urun-plani.md 2.2). It is made from the store's current
/// values and the desired ones (<see cref="Items"/> is the field level diff), shown to the members, and written only when a
/// member with <see cref="RequiredPermission"/> approves it; then a <see cref="StoreJobKind.ApplyChangePlan"/> job applies
/// <see cref="Changes"/>. <see cref="BaseHash"/> fingerprints the touched store values at planning time, so an approval
/// after someone changed the store is refused instead of overwriting. Plans that need a typed confirmation
/// (submission, production release) carry <see cref="ConfirmText"/>.
/// </summary>
[Table(TableNames.ChangePlans)]
[Index(nameof(OrganizationId), nameof(Status))]
[Index(nameof(StoreAppId))]
public class ChangePlan : CreationAuditableEntity<long>
{
    /// <summary>
    /// Column length of <see cref="Title"/> and <see cref="ConfirmText"/>.
    /// </summary>
    public const int TitleMaxLength = 200;

    /// <summary>
    /// Column length of <see cref="DecisionNote"/>.
    /// </summary>
    public const int DecisionNoteMaxLength = 1000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record the plan writes to.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// What the plan changes.
    /// </summary>
    public ChangePlanKind Kind { get; set; }

    /// <summary>
    /// Stored state (Pending, Approved, Applied, Rejected, Discarded); Failed and Expired are derived when read.
    /// </summary>
    public ChangePlanStatus Status { get; set; } = ChangePlanStatus.Pending;

    /// <summary>
    /// One line description.
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string Title { get; set; }

    /// <summary>
    /// Where the plan was made (web, MCP, API, CI).
    /// </summary>
    public ActionChannel Source { get; set; }

    /// <summary>
    /// Member who made the plan.
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// User name of the member who made the plan.
    /// </summary>
    [MaxLength(100)]
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Permission needed to approve (apply) the plan.
    /// </summary>
    public OrgPermission RequiredPermission { get; set; }

    /// <summary>
    /// Text the approver must type (e.g. "Kumbara 1.0"); null when no typed confirmation is needed.
    /// </summary>
    [MaxLength(TitleMaxLength)]
    public string ConfirmText { get; set; }

    /// <summary>
    /// Store version the plan writes to (App Store version id), if one.
    /// </summary>
    [MaxLength(100)]
    public string VersionId { get; set; }

    /// <summary>
    /// User facing version string of <see cref="VersionId"/>.
    /// </summary>
    [MaxLength(100)]
    public string VersionString { get; set; }

    /// <summary>
    /// Field level diff.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<PlanItem> Items { get; set; }

    /// <summary>
    /// Non blocking remarks.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<PlanWarning> Warnings { get; set; }

    /// <summary>
    /// Store calls the apply job makes.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<PlanCall> Calls { get; set; }

    /// <summary>
    /// Check results of the uploaded files of a media plan.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<PlanFile> Files { get; set; }

    /// <summary>
    /// What the apply job writes, as JSON. Never a secret: secrets (the demo account password) are resolved by the job
    /// from their sealed column.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string Changes { get; set; }

    /// <summary>
    /// Content keys whose store values <see cref="BaseHash"/> fingerprints.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> BaseKeys { get; set; }

    /// <summary>
    /// SHA-256 of the touched store values at planning time.
    /// </summary>
    [MaxLength(64)]
    public string BaseHash { get; set; }

    /// <summary>
    /// Rules catalog version the plan was checked against.
    /// </summary>
    [MaxLength(20)]
    public string CatalogVersion { get; set; }

    /// <summary>
    /// Member who approved, rejected or discarded the plan.
    /// </summary>
    public int? DecidedByUserId { get; set; }

    /// <summary>
    /// User name of <see cref="DecidedByUserId"/>.
    /// </summary>
    [MaxLength(100)]
    public string DecidedByUserName { get; set; }

    /// <summary>
    /// Channel of the decision.
    /// </summary>
    public ActionChannel? DecisionChannel { get; set; }

    /// <summary>
    /// Reason of a rejection.
    /// </summary>
    [MaxLength(DecisionNoteMaxLength)]
    public string DecisionNote { get; set; }

    /// <summary>
    /// UTC instant of the decision.
    /// </summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>
    /// UTC instant after which a pending plan can no longer be approved.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// The apply job, once approved.
    /// </summary>
    public long? StoreJobId { get; set; }

    /// <summary>
    /// UTC instant the store accepted every change.
    /// </summary>
    public DateTime? AppliedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
