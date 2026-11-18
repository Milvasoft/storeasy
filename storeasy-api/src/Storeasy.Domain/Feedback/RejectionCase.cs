using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Feedback;

/// <summary>
/// A store rejection being worked on (docs/urun-plani.md 11.3): opened when a version goes to REJECTED or METADATA_REJECTED,
/// the review text is pasted (no API returns it), the cited guidelines become a checklist, a reply is drafted against the
/// Resolution Center limit and sent by hand (or by an assisted step). A resubmission closes it; it stays as history.
/// </summary>
[Table(TableNames.RejectionCases)]
[Index(nameof(OrganizationId), nameof(AppId))]
[Index(nameof(StoreAppId), nameof(State))]
public class RejectionCase : AuditableEntity<int>
{
    /// <summary>
    /// Longest reply App Review accepts in the Resolution Center (catalog rule <c>apple.review.resolution-center-reply</c>).
    /// </summary>
    public const int ReplyMaxLength = 4000;

    /// <summary>
    /// Longest draft stored (a draft may exceed <see cref="ReplyMaxLength"/> while it is being shortened).
    /// </summary>
    public const int DraftMaxLength = 20000;

    /// <summary>
    /// Longest pasted review text stored.
    /// </summary>
    public const int RawTextMaxLength = 50000;

    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 100;

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
    /// The store's id of the rejected version (or submission).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalVersionId { get; set; }

    /// <summary>
    /// User facing version.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string VersionString { get; set; }

    /// <summary>
    /// Build number, if known.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string BuildNumber { get; set; }

    /// <summary>
    /// What was rejected.
    /// </summary>
    public RejectionKind Kind { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public RejectionState State { get; set; } = RejectionState.Open;

    /// <summary>
    /// How the case was opened.
    /// </summary>
    public RejectionSource OpenedVia { get; set; }

    /// <summary>
    /// UTC instant the case was opened.
    /// </summary>
    public DateTime OpenedDate { get; set; }

    /// <summary>
    /// UTC instant the case was closed.
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// Why it was closed.
    /// </summary>
    public RejectionCloseReason? ClosedVia { get; set; }

    /// <summary>
    /// Note given when closed by hand.
    /// </summary>
    [MaxLength(500)]
    public string CloseNote { get; set; }

    /// <summary>
    /// The review text as pasted.
    /// </summary>
    [MaxLength(RawTextMaxLength)]
    public string RawText { get; set; }

    /// <summary>
    /// Who pasted the text.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string RawTextByUserName { get; set; }

    /// <summary>
    /// UTC instant the text was pasted.
    /// </summary>
    public DateTime? RawTextDate { get; set; }

    /// <summary>
    /// Guidelines cited in the text.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<RejectionGuideline> Guidelines { get; set; } = [];

    /// <summary>
    /// Fix items.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<RejectionChecklistItem> Checklist { get; set; } = [];

    /// <summary>
    /// Draft of the Resolution Center reply.
    /// </summary>
    [MaxLength(DraftMaxLength)]
    public string ReplyDraft { get; set; }

    /// <summary>
    /// Who wrote the draft last.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ReplyDraftByUserName { get; set; }

    /// <summary>
    /// Channel the draft was written from (web or MCP).
    /// </summary>
    public ActionChannel? ReplyDraftChannel { get; set; }

    /// <summary>
    /// UTC instant the draft was saved last.
    /// </summary>
    public DateTime? ReplyDraftDate { get; set; }

    /// <summary>
    /// UTC instant a member marked the reply as sent in the Resolution Center.
    /// </summary>
    public DateTime? ReplySentDate { get; set; }

    /// <summary>
    /// Who marked the reply as sent.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ReplySentByUserName { get; set; }

    /// <summary>
    /// The earlier case of the same version, when it was rejected again.
    /// </summary>
    public int? PreviousCaseId { get; set; }

    /// <summary>
    /// What happened, in order.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<RejectionTimelineEntry> Timeline { get; set; } = [];

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    public virtual Apps.App App { get; set; }
}
