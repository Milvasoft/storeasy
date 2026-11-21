using Storeasy.Domain.Content;

namespace Storeasy.Application.Utils.Aso;

/// <summary>
/// How stored rows become impact events: which change plans count (store page changes and production releases), the
/// event kind of a plan, the effect day of an App Store change (the day its version went live, when known) and the event
/// ids. Pure, so the sources stay thin and the rules are unit tested.
/// </summary>
public static class ImpactEventRules
{
    /// <summary>
    /// Id prefix of change plan events.
    /// </summary>
    public const string PlanPrefix = "plan-";

    /// <summary>
    /// Id prefix of App Store version release events.
    /// </summary>
    public const string VersionPrefix = "version-";

    /// <summary>
    /// Id prefix of Microsoft Store submission release events.
    /// </summary>
    public const string MsSubmissionPrefix = "ms-";

    /// <summary>
    /// Id prefix of store listing experiment events (<c>experiment-7-start</c>, <c>experiment-7-end</c>).
    /// </summary>
    public const string ExperimentPrefix = "experiment-";

    /// <summary>
    /// Days before the requested range a plan may have been applied and still go live inside it (an App Store version can
    /// wait a while for review and release).
    /// </summary>
    public const int ReleaseLookbackDays = 120;

    /// <summary>
    /// Change plan kinds that can become an event.
    /// </summary>
    public static IReadOnlyList<ChangePlanKind> PlanKinds { get; } =
    [
        ChangePlanKind.Listing,
        ChangePlanKind.Categories,
        ChangePlanKind.Pricing,
        ChangePlanKind.Screenshots,
        ChangePlanKind.Previews,
        ChangePlanKind.TrackRelease,
        ChangePlanKind.BundleUpload,
        ChangePlanKind.MsChange,
    ];

    // App Store version states (appVersionState and the older appStoreState names).
    private static readonly HashSet<string> _appleLive = new(StringComparer.OrdinalIgnoreCase) { "READY_FOR_SALE", "READY_FOR_DISTRIBUTION" };

    private static readonly HashSet<string> _appleAfterLive = new(StringComparer.OrdinalIgnoreCase)
    {
        "REPLACED_WITH_NEW_VERSION", "REMOVED_FROM_SALE", "DEVELOPER_REMOVED_FROM_SALE", "NOT_APPLICABLE",
    };

    /// <summary>
    /// Whether an App Store version state means the version is live.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool IsAppleLive(string state) => state != null && _appleLive.Contains(state);

    /// <summary>
    /// Whether an App Store version state means the version is not live yet (being prepared, in review, waiting for
    /// release, rejected).
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool IsApplePreLive(string state) => !string.IsNullOrEmpty(state) && !_appleLive.Contains(state) && !_appleAfterLive.Contains(state);

    /// <summary>
    /// The event kind of an applied change plan, or null when the plan does not change the store page (review
    /// information, testers, a test track release, a package only Microsoft change...).
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="items"></param>
    /// <returns></returns>
    public static ImpactEventKind? KindOf(ChangePlanKind kind, IReadOnlyList<PlanItem> items)
    {
        items ??= [];

        return kind switch
        {
            ChangePlanKind.Listing => ImpactEventKind.Listing,
            ChangePlanKind.Categories => ImpactEventKind.Category,
            ChangePlanKind.Pricing => ImpactEventKind.Price,
            ChangePlanKind.Previews => ImpactEventKind.Previews,
            ChangePlanKind.Screenshots => items.Count > 0 && items.All(i => IsIcon(i.Field)) ? ImpactEventKind.Icon : ImpactEventKind.Screenshots,
            ChangePlanKind.TrackRelease or ChangePlanKind.BundleUpload => items.Any(i => i.Group == ContentGroups.Track && ChangePlanRules.IsProduction(i.Field)) ? ImpactEventKind.VersionReleased : null,
            ChangePlanKind.MsChange => MsKindOf(items),
            _ => null,
        };
    }

    /// <summary>
    /// The effect instant of an App Store change written to a version: the day the version went live when it went live
    /// after the write; the write itself when the version was already live (an edit of the live version) or when its live
    /// day is not known; awaiting release while the version is not live yet.
    /// </summary>
    /// <param name="appliedDate">When the store accepted the write.</param>
    /// <param name="versionState">Last known state of the version; null when not known.</param>
    /// <param name="stateChangedDate">When the version reached <paramref name="versionState"/>.</param>
    /// <returns></returns>
    public static (DateTime At, ImpactDateSource Source) AppleEffect(DateTime appliedDate, string versionState, DateTime? stateChangedDate)
    {
        if (IsAppleLive(versionState) && stateChangedDate.HasValue)
            return stateChangedDate.Value >= appliedDate ? (stateChangedDate.Value, ImpactDateSource.StoreLive) : (appliedDate, ImpactDateSource.Applied);

        return IsApplePreLive(versionState) ? (appliedDate, ImpactDateSource.AwaitingRelease) : (appliedDate, ImpactDateSource.Applied);
    }

    /// <summary>
    /// Locales of the items, sorted, without empties.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static List<string> Locales(IReadOnlyList<PlanItem> items)
        => [.. (items ?? []).Select(i => i.Locale).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];

    /// <summary>
    /// Fields of the items (an order change counts as its field), sorted, without empties.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static List<string> Fields(IReadOnlyList<PlanItem> items)
        => [.. (items ?? []).Select(i => i.Field)
                            .Where(f => !string.IsNullOrWhiteSpace(f))
                            .Select(f => f.EndsWith(".order", StringComparison.Ordinal) ? f[..^".order".Length] : f)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// When a Microsoft Store submission was first seen published; null when it never was.
    /// </summary>
    /// <param name="history"></param>
    /// <returns></returns>
    public static DateTime? MsPublishedDate(IReadOnlyList<MsStatusChange> history)
        => history?.Where(h => h.Status == MsSubmissionStatus.Published).OrderBy(h => h.Date).Select(h => (DateTime?)h.Date).FirstOrDefault();

    /// <summary>
    /// UTC day of an instant.
    /// </summary>
    /// <param name="instant"></param>
    /// <returns></returns>
    public static DateOnly Day(DateTime instant) => DateOnly.FromDateTime(instant.Kind == DateTimeKind.Local ? instant.ToUniversalTime() : instant);

    /// <summary>
    /// Merges the events of every source: one per id (the first source wins), newest first.
    /// </summary>
    /// <param name="lists"></param>
    /// <returns></returns>
    public static List<ImpactEvent> Merge(IEnumerable<IEnumerable<ImpactEvent>> lists)
        => [.. lists.SelectMany(l => l ?? [])
                    .Where(e => e != null && !string.IsNullOrEmpty(e.Id))
                    .GroupBy(e => e.Id, StringComparer.Ordinal)
                    .Select(g => g.First())
                    .OrderByDescending(e => e.Date)
                    .ThenByDescending(e => e.At ?? DateTime.MinValue)
                    .ThenBy(e => e.Id, StringComparer.Ordinal)];

    private static bool IsIcon(string field) => field != null && (field.Equals("icon", StringComparison.OrdinalIgnoreCase) || field.Equals("icon.order", StringComparison.OrdinalIgnoreCase));

    private static ImpactEventKind? MsKindOf(IReadOnlyList<PlanItem> items)
    {
        var kinds = items.Select(i => i.Group?.ToLowerInvariant() switch
        {
            "listing" => ImpactEventKind.Listing,
            "image" => i.Field != null && (i.Field.Contains("logo", StringComparison.OrdinalIgnoreCase) || i.Field.Contains("icon", StringComparison.OrdinalIgnoreCase)) ? ImpactEventKind.Icon : ImpactEventKind.Screenshots,
            "pricing" or "availability" => ImpactEventKind.Price,
            _ => (ImpactEventKind?)null,
        }).Where(k => k.HasValue).Distinct().ToList();

        return kinds.Count switch
        {
            0 => null,
            1 => kinds[0],
            _ => ImpactEventKind.MsChange,
        };
    }
}
