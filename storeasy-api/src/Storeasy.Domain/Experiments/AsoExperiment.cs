using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Experiments;

/// <summary>
/// A store listing experiment or custom page of an app in one store, kept in storeasy so a before/after impact report can
/// lay experiments over the metric charts. App Store experiments and custom product pages are written through change
/// plans and their state is read back by the sync; Play experiments and custom listings have no API, so the member runs
/// them in Play Console and records start, end and result here.
/// </summary>
[Table(TableNames.AsoExperiments)]
[Index(nameof(OrganizationId), nameof(AppId))]
[Index(nameof(StoreAppId), nameof(ExternalId))]
public class AsoExperiment : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 200;

    /// <summary>
    /// Column length of <see cref="ResultSummary"/> and <see cref="Note"/>.
    /// </summary>
    public const int TextMaxLength = 2000;

    /// <summary>
    /// Column length of the addresses.
    /// </summary>
    public const int UrlMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// What it is.
    /// </summary>
    public AsoExperimentKind Kind { get; set; }

    /// <summary>
    /// Name (the store's reference name).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public AsoExperimentStatus Status { get; set; } = AsoExperimentStatus.Draft;

    /// <summary>
    /// The store's own state, as read (App Store experiment or custom product page version state).
    /// </summary>
    [MaxLength(50)]
    public string StoreState { get; set; }

    /// <summary>
    /// The store's id (App Store experiment or custom product page id), once written.
    /// </summary>
    [MaxLength(100)]
    public string ExternalId { get; set; }

    /// <summary>
    /// App Store custom product page version being edited or live.
    /// </summary>
    [MaxLength(100)]
    public string ExternalVersionId { get; set; }

    /// <summary>
    /// Share of the traffic the treatments get, in percent (product page optimization, Play experiments).
    /// </summary>
    public int? TrafficProportion { get; set; }

    /// <summary>
    /// Whether App Review must approve the experiment before it can start (App Store; null until read).
    /// </summary>
    public bool? ReviewRequired { get; set; }

    /// <summary>
    /// Variants and what they change.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<AsoExperimentVariant> Variants { get; set; } = [];

    /// <summary>
    /// UTC instant it started getting traffic.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// UTC instant it ended.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Address of the page (custom product page address with its <c>ppid</c>, Play custom listing address).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string Url { get; set; }

    /// <summary>
    /// Result in the member's words (App Store results are only shown in App Store Connect).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ResultSummary { get; set; }

    /// <summary>
    /// Link to the results (store console page, report).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string ResultUrl { get; set; }

    /// <summary>
    /// Key of the winning variant, when known.
    /// </summary>
    [MaxLength(20)]
    public string WinnerVariantKey { get; set; }

    /// <summary>
    /// Free note.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Note { get; set; }

    /// <summary>
    /// Done items of the assisted checklist (Play).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<int> ChecklistDone { get; set; } = [];

    /// <summary>
    /// Change plans made for it, oldest first.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<long> PlanIds { get; set; } = [];

    /// <summary>
    /// UTC instant it was submitted for App Review.
    /// </summary>
    public DateTime? SubmittedDate { get; set; }

    /// <summary>
    /// UTC instant the store state was last read.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
