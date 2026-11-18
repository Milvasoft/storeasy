using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Rules;
using System.Globalization;

namespace Storeasy.Infrastructure.Rules;

/// <summary>
/// Default <see cref="IRuleImpactFinder"/>: reads the store records of every organization (<see cref="IStoreContentService"/>)
/// and evaluates the rules against each record's subject; a rule that does not apply is skipped, an unknown one is kept.
/// </summary>
/// <param name="organizationRepository"></param>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
public sealed class RuleImpactFinder(IStoreasyRepositoryBase<Organization> organizationRepository, IStoreContentService contentService, IRulesCatalog rulesCatalog) : IRuleImpactFinder
{
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = organizationRepository;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RuleImpact>> FindAsync(IReadOnlyList<string> ruleIds, CancellationToken cancellationToken)
    {
        var rules = (ruleIds ?? []).Select(id => _rulesCatalog.Current?.Find(id)).Where(r => r != null).ToList();

        if (rules.Count == 0)
            return [];

        var organizations = await _organizationRepository.GetAllAsync(o => !o.IsDeleted, projection: o => new Organization { Id = o.Id }, cancellationToken: cancellationToken);
        var language = CultureInfo.CurrentUICulture.Name;
        var impact = new List<RuleImpact>();

        foreach (var organization in organizations)
        {
            var records = await _contentService.GetStoreAppsAsync(organization.Id, null, cancellationToken);

            foreach (var record in records)
            {
                var subject = _contentService.Subject(record, null);

                impact.AddRange(rules.Where(rule => rule.StoreKind == record.Store)
                                     .Select(rule => (Rule: rule, Match: RuleEvaluator.Evaluate(rule, subject)))
                                     .Where(x => x.Match.Applicability != RuleApplicability.DoesNotApply)
                                     .Select(x => new RuleImpact(record.OrganizationId, record.AppId, record.AppName, x.Rule.Id, $"{x.Rule.Title?.In(language)} ({string.Join("; ", x.Match.Reasons)})")));
            }
        }

        return [.. impact.DistinctBy(i => (i.AppId, i.RuleId))];
    }
}
