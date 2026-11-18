using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleProposalDetail;

/// <summary>
/// Reads one catalog proposal with the source diff, the catalog patch and the apps it affects.
/// </summary>
public record GetRuleProposalDetailQuery : IQuery<RuleProposalDetailDto>
{
    /// <summary>
    /// Proposal.
    /// </summary>
    public int ProposalId { get; set; }
}
