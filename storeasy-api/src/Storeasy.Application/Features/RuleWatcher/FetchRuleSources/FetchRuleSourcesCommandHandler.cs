using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.RuleWatcher.FetchRuleSources;

/// <summary>
/// Handles <see cref="FetchRuleSourcesCommand"/>.
/// </summary>
/// <param name="RuleWatcher"></param>
public record FetchRuleSourcesCommandHandler(IRuleWatcher RuleWatcher) : IInterceptable, ICommandHandler<FetchRuleSourcesCommand, RuleWatchResultDto>
{
    private readonly IRuleWatcher _ruleWatcher = RuleWatcher;

    /// <inheritdoc/>
    public async Task<Response<RuleWatchResultDto>> Handle(FetchRuleSourcesCommand request, CancellationToken cancellationToken)
    {
        var result = await _ruleWatcher.RunAsync(cancellationToken);

        return Response<RuleWatchResultDto>.Success(new RuleWatchResultDto
        {
            Checked = result.Checked,
            Changed = result.Changed,
            Unreachable = result.Unreachable,
            ProposalsOpened = result.ProposalsOpened,
        });
    }
}
