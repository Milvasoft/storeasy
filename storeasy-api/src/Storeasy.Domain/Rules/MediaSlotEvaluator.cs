using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Rules;

/// <summary>
/// The verdict on one media class (slot) of a store: whether it is required, how it is filled and how many items it has.
/// </summary>
/// <param name="RuleId">Catalog rule of the class.</param>
/// <param name="Required">Whether the class is required on the evaluated day.</param>
/// <param name="Fill">How the class is filled.</param>
/// <param name="FallbackFromRuleId">Class the store derives this one from, when <see cref="MediaSlotFill.Fallback"/>.</param>
/// <param name="Count">Own items.</param>
public sealed record MediaSlot(string RuleId, bool Required, MediaSlotFill Fill, string FallbackFromRuleId, int Count);

/// <summary>
/// Catalog driven media slot evaluation (docs/urun-plani.md 12.2): a class with enough own items is <see cref="MediaSlotFill.Own"/>;
/// otherwise a class named in <c>satisfiedBy</c> or reachable through the <c>fallbackTo</c> chain (followed recursively)
/// with own items fills it (<see cref="MediaSlotFill.Fallback"/>); otherwise it is empty. Pure: the caller passes the
/// applicable rules and the own item counts per rule.
/// </summary>
public static class MediaSlotEvaluator
{
    /// <summary>
    /// Evaluates every rule of <paramref name="rules"/> (the applicable media class rules of one store and locale).
    /// </summary>
    /// <param name="rules"></param>
    /// <param name="counts">Own item count per rule id.</param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static List<MediaSlot> Evaluate(IReadOnlyList<CatalogRule> rules, IReadOnlyDictionary<string, int> counts, DateOnly today)
    {
        var byId = rules.ToDictionary(r => r.Id);

        bool HasOwn(string id) => byId.TryGetValue(id, out var r) && counts.TryGetValue(id, out var c) && c >= Math.Max(1, r.Params?.MinCount ?? 1);

        string FindFallback(CatalogRule rule)
        {
            foreach (var id in rule.Requirement?.SatisfiedBy ?? [])
            {
                if (HasOwn(id))
                    return id;
            }

            var visited = new HashSet<string> { rule.Id };
            var queue = new Queue<string>(rule.Requirement?.FallbackTo ?? []);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();

                if (!visited.Add(id))
                    continue;

                if (HasOwn(id))
                    return id;

                if (byId.TryGetValue(id, out var next))
                {
                    foreach (var further in next.Requirement?.FallbackTo ?? [])
                        queue.Enqueue(further);
                }
            }

            return null;
        }

        return [.. rules.Select(rule =>
        {
            var count = counts.TryGetValue(rule.Id, out var c) ? c : 0;
            var required = RuleEvaluator.IsRequiredOn(rule, today);

            if (HasOwn(rule.Id))
                return new MediaSlot(rule.Id, required, MediaSlotFill.Own, null, count);

            var fallback = FindFallback(rule);

            return fallback != null
                ? new MediaSlot(rule.Id, required, MediaSlotFill.Fallback, fallback, count)
                : new MediaSlot(rule.Id, required, MediaSlotFill.None, null, count);
        })];
    }
}
