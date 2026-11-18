using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.RuleWatcher.RejectRuleProposal;

/// <summary>
/// Rejects a pending proposal with a reason. The source stays watched; the same content never opens another proposal.
/// </summary>
public record RejectRuleProposalCommand : ICommand<int>
{
    /// <summary>
    /// Proposal.
    /// </summary>
    public int ProposalId { get; set; }

    /// <summary>
    /// Reason.
    /// </summary>
    public RuleProposalRejectReason Reason { get; set; }

    /// <summary>
    /// Note.
    /// </summary>
    public string Note { get; set; }
}
