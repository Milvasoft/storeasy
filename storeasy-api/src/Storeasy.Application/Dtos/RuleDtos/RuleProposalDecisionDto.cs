namespace Storeasy.Application.Dtos.RuleDtos;

/// <summary>
/// Result of approving a proposal.
/// </summary>
public class RuleProposalDecisionDto
{
    /// <summary>Catalog version published (null when the catalog is file based and the patch goes in by a pull request).</summary>
    public string CatalogVersion { get; set; }

    /// <summary>Organizations notified.</summary>
    public int NotifiedOrganizationCount { get; set; }

    /// <summary>Users notified.</summary>
    public int NotifiedUserCount { get; set; }
}
