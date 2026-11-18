using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleDetail;

/// <summary>
/// Handles <see cref="GetRuleDetailQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetRuleDetailQueryHandler(IStoreContentService contentService,
                                       IRulesCatalog rulesCatalog,
                                       TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetRuleDetailQuery, RuleDetailDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<RuleDetailDto>> Handle(GetRuleDetailQuery request, CancellationToken cancellationToken)
    {
        var rule = _rulesCatalog.Current.Find(request.RuleId);

        if (rule == null)
            return Response<RuleDetailDto>.Error(null, MessageKey.RuleNotFound);

        var language = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var affected = (await _contentService.GetStoreAppsAsync(request.OrganizationId, null, cancellationToken)).Where(a => a.Store == rule.StoreKind)
                                                                                                             .Select(a => (App: a, Match: RuleEvaluator.Evaluate(rule, _contentService.Subject(a, null))))
                                                                                                             .Where(x => x.Match.Applicability != RuleApplicability.DoesNotApply)
                                                                                                             .Select(x => new RuleAffectedAppDto
                                                                                                             {
                                                                                                                 AppId = x.App.AppId,
                                                                                                                 Name = x.App.AppName,
                                                                                                                 Applicability = x.Match.Applicability,
                                                                                                                 Reasons = [.. x.Match.Reasons],
                                                                                                                 UnknownKeys = [.. x.Match.UnknownKeys],
                                                                                                             })
                                                                                                             .ToList();

        var detail = ContentViews.Fill(new RuleDetailDto(), rule, today, language, affected.Count);

        detail.Description = rule.Description?.In(language);
        detail.Condition = rule.Requirement?.Condition;
        detail.FallbackChain = rule.Requirement?.FallbackTo ?? [];
        detail.SatisfiedBy = rule.Requirement?.SatisfiedBy ?? [];
        detail.AppliesWhen = rule.AppliesWhen;
        detail.Params = rule.Params;
        detail.Sources = rule.Sources ?? [];
        detail.Conflicts = rule.Conflicts ?? [];
        detail.Notes = rule.Notes;
        detail.DateNote = rule.DateNote;
        detail.CatalogVersion = _rulesCatalog.Current.Version;
        detail.AffectedApps = affected;

        return Response<RuleDetailDto>.Success(detail);
    }
}
