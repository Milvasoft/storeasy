using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Rules;

/// <summary>
/// A proposed change of the rules catalog for a platform admin to decide (nothing is published automatically). The watcher
/// opens it with the source diff; Claude through MCP may add the rule ids and the catalog patch.
/// </summary>
[Table(TableNames.RuleProposals)]
[Index(nameof(Status))]
[Index(nameof(RuleSourceId), nameof(ContentHash), IsUnique = true)]
public class RuleProposal : BaseEntity<int>
{
    /// <summary>
    /// Longest excerpt or diff stored.
    /// </summary>
    public const int ExcerptMaxLength = 20000;

    /// <summary>
    /// Title.
    /// </summary>
    [MaxLength(300)]
    public string Title { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// Source whose change opened it.
    /// </summary>
    public int? RuleSourceId { get; set; }

    /// <summary>
    /// Hash of the source content the proposal is about (a rejected hash never opens a second proposal).
    /// </summary>
    [MaxLength(64)]
    public string ContentHash { get; set; }

    /// <summary>
    /// Hash before the change.
    /// </summary>
    [MaxLength(64)]
    public string PreviousContentHash { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public RuleProposalStatus Status { get; set; } = RuleProposalStatus.Pending;

    /// <summary>
    /// Who prepared it.
    /// </summary>
    public RuleProposalPreparer PreparedBy { get; set; } = RuleProposalPreparer.Watcher;

    /// <summary>
    /// User whose token prepared the patch (MCP).
    /// </summary>
    [MaxLength(100)]
    public string PreparedByUserName { get; set; }

    /// <summary>
    /// Catalog rules the change affects.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> RuleIds { get; set; } = [];

    /// <summary>
    /// Removed lines of the source (diff, before).
    /// </summary>
    [MaxLength(ExcerptMaxLength)]
    public string SourceExcerptBefore { get; set; }

    /// <summary>
    /// Added lines of the source (diff, after).
    /// </summary>
    [MaxLength(ExcerptMaxLength)]
    public string SourceExcerptAfter { get; set; }

    /// <summary>
    /// Proposed catalog change (JSON patch or the changed rule objects), when prepared.
    /// </summary>
    [MaxLength(ExcerptMaxLength)]
    public string CatalogPatch { get; set; }

    /// <summary>
    /// Note of the preparer.
    /// </summary>
    [MaxLength(2000)]
    public string PreparerNote { get; set; }

    /// <summary>
    /// Catalog version when the proposal was opened.
    /// </summary>
    [MaxLength(20)]
    public string CatalogVersionFrom { get; set; }

    /// <summary>
    /// Catalog version published by the approval.
    /// </summary>
    [MaxLength(20)]
    public string CatalogVersionTo { get; set; }

    /// <summary>
    /// Why it was rejected.
    /// </summary>
    public RuleProposalRejectReason? RejectReason { get; set; }

    /// <summary>
    /// Decision note.
    /// </summary>
    [MaxLength(1000)]
    public string DecisionNote { get; set; }

    /// <summary>
    /// Who decided.
    /// </summary>
    [MaxLength(100)]
    public string DecidedByUserName { get; set; }

    /// <summary>
    /// UTC instant of the decision.
    /// </summary>
    public DateTime? DecidedDate { get; set; }

    /// <summary>
    /// Organizations notified on approval.
    /// </summary>
    public int NotifiedOrganizationCount { get; set; }

    /// <summary>
    /// UTC instant it was opened.
    /// </summary>
    public DateTime CreationDate { get; set; }
}
