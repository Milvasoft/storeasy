using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.ApproveRuleProposal;

/// <summary>
/// Approves a pending proposal (the catalog patch is published with the next catalog version of the repository) and, with
/// <see cref="Notify"/>, the owners and admins of every affected app are told.
/// </summary>
public record ApproveRuleProposalCommand : ICommand<RuleProposalDecisionDto>
{
    /// <summary>
    /// Proposal.
    /// </summary>
    public int ProposalId { get; set; }

    /// <summary>
    /// Notify the affected apps' owners and admins.
    /// </summary>
    public bool Notify { get; set; } = true;

    /// <summary>
    /// Note.
    /// </summary>
    public string Note { get; set; }
}
