using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;

namespace Storeasy.Application.Features.Rules.GetRuleCatalog;

/// <summary>
/// Handles <see cref="GetRuleCatalogQuery"/>.
/// </summary>
/// <param name="rulesCatalog"></param>
public class GetRuleCatalogQueryHandler(IRulesCatalog rulesCatalog) : IInterceptable, IQueryHandler<GetRuleCatalogQuery, RuleCatalogInfoDto>
{
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;

    /// <inheritdoc/>
    public Task<Response<RuleCatalogInfoDto>> Handle(GetRuleCatalogQuery request, CancellationToken cancellationToken)
    {
        var catalog = _rulesCatalog.Current;

        return Task.FromResult(Response<RuleCatalogInfoDto>.Success(new RuleCatalogInfoDto
        {
            Version = catalog.Version,
            PublishedDate = catalog.GeneratedAt,
            RuleCount = catalog.Rules.Count,
            Source = catalog.Source,
        }));
    }
}
