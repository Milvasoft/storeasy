using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.RuleWatcher.PrepareRuleProposal;

/// <summary>
/// Writes the catalog side of a pending proposal: the rules it changes and the patch (what the MCP tool
/// <c>rules_proposal_prepare</c> calls with the caller's token). Nothing is published until an admin approves.
/// </summary>
public record PrepareRuleProposalCommand : ICommand<int>
{
    /// <summary>
    /// Proposal.
    /// </summary>
    public int ProposalId { get; set; }

    /// <summary>
    /// Catalog rule ids the change affects.
    /// </summary>
    public List<string> RuleIds { get; set; } = [];

    /// <summary>
    /// The catalog change (JSON).
    /// </summary>
    public string CatalogPatch { get; set; }

    /// <summary>
    /// Note for the reviewer.
    /// </summary>
    public string Note { get; set; }
}
