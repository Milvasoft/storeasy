using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleSourceList;

/// <summary>
/// Handles <see cref="GetRuleSourceListQuery"/>. Configured sources missing from the database are added first.
/// </summary>
/// <param name="SourceRepository"></param>
/// <param name="RuleWatcher"></param>
public record GetRuleSourceListQueryHandler(IStoreasyRepositoryBase<RuleSource> SourceRepository, IRuleWatcher RuleWatcher) : IInterceptable, IQueryHandler<GetRuleSourceListQuery, List<RuleSourceDto>>
{
    private readonly IStoreasyRepositoryBase<RuleSource> _sourceRepository = SourceRepository;
    private readonly IRuleWatcher _ruleWatcher = RuleWatcher;

    /// <inheritdoc/>
    public async Task<Response<List<RuleSourceDto>>> Handle(GetRuleSourceListQuery request, CancellationToken cancellationToken)
    {
        await _ruleWatcher.EnsureSourcesAsync(cancellationToken);

        var sources = await _sourceRepository.GetAllAsync(s => s.Id > 0, projection: RuleSourceDto.Projection, cancellationToken: cancellationToken);

        return Response<List<RuleSourceDto>>.Success([.. sources.OrderBy(s => s.Store).ThenBy(s => s.Name)]);
    }
}
