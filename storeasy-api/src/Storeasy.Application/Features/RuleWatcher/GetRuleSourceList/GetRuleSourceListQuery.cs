using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleSourceList;

/// <summary>
/// The official sources the rule watcher reads, with the result of their last read (platform admins).
/// </summary>
public record GetRuleSourceListQuery : IQuery<List<RuleSourceDto>>
{
}
