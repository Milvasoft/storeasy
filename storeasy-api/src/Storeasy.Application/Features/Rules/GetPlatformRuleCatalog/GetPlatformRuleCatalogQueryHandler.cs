using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Rules.GetPlatformRuleCatalog;

/// <summary>
/// Handles <see cref="GetPlatformRuleCatalogQuery"/> from the catalog in memory; no database.
/// </summary>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetPlatformRuleCatalogQueryHandler(IRulesCatalog rulesCatalog, TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetPlatformRuleCatalogQuery, PlatformRuleCatalogDto>
{
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task<Response<PlatformRuleCatalogDto>> Handle(GetPlatformRuleCatalogQuery request, CancellationToken cancellationToken)
    {
        var catalog = _rulesCatalog.Current;
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        return Task.FromResult(Response<PlatformRuleCatalogDto>.Success(new PlatformRuleCatalogDto
        {
            Catalog = new RuleCatalogInfoDto { Version = catalog.Version, PublishedDate = catalog.GeneratedAt, RuleCount = catalog.Rules.Count, Source = catalog.Source },
            Rules = [.. catalog.Rules.Where(r => r.Deprecated == null && (request.Store == null || r.StoreKind == request.Store))
                                     .Select(r => ContentViews.Rule(r, today, language, 0))
                                     .OrderBy(r => r.DueDate ?? DateOnly.MaxValue)
                                     .ThenBy(r => r.Id, StringComparer.Ordinal)],
        }));
    }
}
