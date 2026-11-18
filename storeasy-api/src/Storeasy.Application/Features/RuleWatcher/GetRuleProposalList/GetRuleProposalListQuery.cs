using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleProposalList;

/// <summary>
/// Pages through the catalog proposals, newest first (filter <c>Status</c> for the pending queue).
/// </summary>
public record GetRuleProposalListQuery : ListRequest, IListRequestQuery<RuleProposalListDto>
{
}
