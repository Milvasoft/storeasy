using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Apps;

/// <summary>
/// An assisted step of an app in one store (docs/urun-plani.md 11.4): store work without an API, done by the member or a
/// browser agent in the member's own store session, and recorded here. The checklist comes from the step definition
/// (a catalog rule or a built in step, <see cref="Key"/>); the row keeps its progress, hand-off and result. storeasy never
/// stores or asks for a store password.
/// </summary>
[Table(TableNames.AssistedSteps)]
[Index(nameof(AppId), nameof(Store), nameof(Key), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class AssistedStep : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Key"/>.
    /// </summary>
    public const int KeyMaxLength = 150;

    /// <summary>
    /// Column length of <see cref="Note"/>.
    /// </summary>
    public const int NoteMaxLength = 1000;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store the step is done in.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Step definition: a catalog rule id or a built in step key.
    /// </summary>
    [MaxLength(KeyMaxLength)]
    public string Key { get; set; }

    /// <summary>
    /// Progress.
    /// </summary>
    public AssistedStepStatus Status { get; set; } = AssistedStepStatus.Todo;

    /// <summary>
    /// Positions (from 1) of the checklist items marked done.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<int> DoneItems { get; set; }

    /// <summary>
    /// UTC instant of the last hand-off to a browser agent.
    /// </summary>
    public DateTime? HandedOffDate { get; set; }

    /// <summary>
    /// Member who handed the step off.
    /// </summary>
    [MaxLength(100)]
    public string HandedOffByUserName { get; set; }

    /// <summary>
    /// UTC instant the step was recorded as done or skipped.
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Member who recorded the result.
    /// </summary>
    [MaxLength(100)]
    public string CompletedByUserName { get; set; }

    /// <summary>
    /// Channel the result came from (web, MCP).
    /// </summary>
    public ActionChannel? CompletedChannel { get; set; }

    /// <summary>
    /// Evidence screenshot, if one was uploaded.
    /// </summary>
    public long? EvidenceAssetId { get; set; }

    /// <summary>
    /// Note on the result.
    /// </summary>
    [MaxLength(NoteMaxLength)]
    public string Note { get; set; }

    /// <summary>
    /// Navigation property of the app.
    /// </summary>
    [ForeignKey(nameof(AppId))]
    public virtual App App { get; set; }
}
