namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// A catalog proposal with the source diff, the catalog patch and its impact.
/// </summary>
public class RuleProposalDetailDto : RuleProposalListDto
{
    /// <summary>Source name.</summary>
    public string SourceName { get; set; }

    /// <summary>Source address.</summary>
    public string SourceUrl { get; set; }

    /// <summary>Hash of the content the proposal is about.</summary>
    public string ContentHash { get; set; }

    /// <summary>Hash before the change.</summary>
    public string PreviousContentHash { get; set; }

    /// <summary>Removed lines.</summary>
    public string SourceExcerptBefore { get; set; }

    /// <summary>Added lines.</summary>
    public string SourceExcerptAfter { get; set; }

    /// <summary>Proposed catalog change.</summary>
    public string CatalogPatch { get; set; }

    /// <summary>Preparer's note.</summary>
    public string PreparerNote { get; set; }

    /// <summary>Why it was rejected.</summary>
    public RuleProposalRejectReason? RejectReason { get; set; }

    /// <summary>Decision note.</summary>
    public string DecisionNote { get; set; }

    /// <summary>Who decided.</summary>
    public string DecidedByUserName { get; set; }

    /// <summary>UTC instant decided.</summary>
    public DateTime? DecidedDate { get; set; }

    /// <summary>Organizations notified on approval.</summary>
    public int NotifiedOrganizationCount { get; set; }

    /// <summary>Apps the change affects (from the catalog).</summary>
    public List<CatalogAffectedApp> Impact { get; set; } = [];
}
