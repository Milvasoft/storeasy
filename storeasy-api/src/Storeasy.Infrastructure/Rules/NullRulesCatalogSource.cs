using Storeasy.Application.Interfaces;

namespace Storeasy.Infrastructure.Rules;

/// <summary>
/// The <see cref="IRulesCatalogSource"/> used until the phase 2 catalog loader is wired: it knows no rules, so the
/// compliance reminders stay silent and approvals notify nobody (the watcher and the proposals work regardless).
/// </summary>
public sealed class NullRulesCatalogSource : IRulesCatalogSource
{
    /// <inheritdoc/>
    public Task<string> GetCatalogVersionAsync(CancellationToken cancellationToken) => Task.FromResult<string>(null);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CatalogDeadline>> GetDeadlinesAsync(IReadOnlyList<CatalogAppContext> apps, DateTime from, DateTime to, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CatalogDeadline>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CatalogAffectedApp>> FindAffectedAppsAsync(IReadOnlyList<string> ruleIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CatalogAffectedApp>>([]);

    /// <inheritdoc/>
    public Task<string> ApplyChangeAsync(CatalogChange change, CancellationToken cancellationToken) => Task.FromResult<string>(null);
}
