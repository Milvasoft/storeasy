using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.FetchRuleSources;

/// <summary>
/// Reads every watched source now (the watcher also runs daily) and opens proposals for the changes.
/// </summary>
public record FetchRuleSourcesCommand : ICommand<RuleWatchResultDto>
{
}
