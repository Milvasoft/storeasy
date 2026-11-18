using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// The rules catalog as the tests define it (phase 2's loader is not wired in this phase).
/// </summary>
public sealed class FakeRulesCatalogSource : IRulesCatalogSource
{
    /// <summary>Deadlines by app id.</summary>
    public ConcurrentDictionary<int, List<CatalogDeadline>> Deadlines { get; } = new();

    /// <summary>Affected apps by rule id.</summary>
    public ConcurrentDictionary<string, List<CatalogAffectedApp>> Affected { get; } = new();

    /// <summary>Changes applied.</summary>
    public ConcurrentQueue<CatalogChange> Applied { get; } = new();

    /// <inheritdoc/>
    public Task<string> GetCatalogVersionAsync(CancellationToken cancellationToken) => Task.FromResult("1.0.1");

    /// <inheritdoc/>
    public Task<IReadOnlyList<CatalogDeadline>> GetDeadlinesAsync(IReadOnlyList<CatalogAppContext> apps, DateTime from, DateTime to, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CatalogDeadline>>([.. apps.SelectMany(a => Deadlines.TryGetValue(a.AppId, out var list) ? list : []).Where(d => d.DueDate >= from && d.DueDate <= to)]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<CatalogAffectedApp>> FindAffectedAppsAsync(IReadOnlyList<string> ruleIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CatalogAffectedApp>>([.. ruleIds.SelectMany(r => Affected.TryGetValue(r, out var list) ? list : [])]);

    /// <inheritdoc/>
    public Task<string> ApplyChangeAsync(CatalogChange change, CancellationToken cancellationToken)
    {
        Applied.Enqueue(change);

        return Task.FromResult("1.1.0");
    }
}
