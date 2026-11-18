using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleList;

/// <summary>
/// Handles <see cref="GetRuleListQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetRuleListQueryHandler(IStoreContentService contentService,
                                     IRulesCatalog rulesCatalog,
                                     TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetRuleListQuery, List<RuleListDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<RuleListDto>>> Handle(GetRuleListQuery request, CancellationToken cancellationToken)
    {
        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var subjects = (await _contentService.GetStoreAppsAsync(request.OrganizationId, null, cancellationToken)).Select(a => (a.AppId, Subject: _contentService.Subject(a, null))).ToList();

        var rules = _rulesCatalog.Current.Rules.Where(r => r.Deprecated == null)
                                               .Where(r => request.Store == null || r.StoreKind == request.Store)
                                               .Where(r => string.IsNullOrWhiteSpace(request.Category) || r.Category == request.Category)
                                               .Select(r => ContentViews.Rule(r, today, language, subjects.Where(s => RuleEvaluator.Evaluate(r, s.Subject).Applicability != RuleApplicability.DoesNotApply)
                                                                                                            .Select(s => s.AppId)
                                                                                                            .Distinct()
                                                                                                            .Count()))
                                               .Where(r => request.Status == null || r.Status == request.Status)
                                               .Where(r => string.IsNullOrWhiteSpace(request.Search)
                                                           || r.Id.Contains(request.Search, StringComparison.OrdinalIgnoreCase)
                                                           || (r.Title ?? string.Empty).Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                                               .OrderBy(r => r.DueDate ?? DateOnly.MaxValue)
                                               .ThenBy(r => r.Id, StringComparer.Ordinal)
                                               .ToList();

        return Response<List<RuleListDto>>.Success(rules);
    }
}
