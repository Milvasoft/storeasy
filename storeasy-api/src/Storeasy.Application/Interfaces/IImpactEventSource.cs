using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// One source of change events for the before and after impact report (applied change plans, released versions, and any
/// later source such as store listing experiments). Every implementation registered in DI is read and the results are
/// merged by <see cref="ImpactEvent.Id"/>, so a new source only needs a class and one registration. Sources read
/// storeasy's own rows; none calls a store.
/// </summary>
public interface IImpactEventSource
{
    /// <summary>
    /// The events of the query's organization (and app or store record) whose effect day is in the range.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Computes before and after impact reports of change events from the stored metrics and keyword ranks.
/// </summary>
public interface IAsoImpactAnalyzer
{
    /// <summary>
    /// The events of every <see cref="IImpactEventSource"/>, newest first, without duplicates.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<List<ImpactEvent>> GetEventsAsync(ImpactEventQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The report of one event; <paramref name="others"/> are the other events of the app (for the confounding check).
    /// </summary>
    /// <param name="impactEvent"></param>
    /// <param name="others"></param>
    /// <param name="options"></param>
    /// <param name="includeRanks">Whether to read the tracked keywords' ranks too.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AsoImpactReportDto> AnalyzeAsync(ImpactEvent impactEvent, IReadOnlyList<ImpactEvent> others, ImpactOptions options, bool includeRanks, CancellationToken cancellationToken);
}

/// <summary>
/// Which events a source returns.
/// </summary>
/// <param name="OrganizationId">Organization (always checked).</param>
/// <param name="AppId">Only this app; null for every app of the organization.</param>
/// <param name="StoreAppId">Only this store record.</param>
/// <param name="From">First effect day.</param>
/// <param name="To">Last effect day.</param>
public sealed record ImpactEventQuery(int OrganizationId, int? AppId, int? StoreAppId, DateOnly From, DateOnly To);

/// <summary>
/// A change that may have moved the store page metrics.
/// </summary>
public sealed record ImpactEvent
{
    /// <summary>
    /// Stable id: <c>plan-12</c>, <c>version-34</c>, <c>ms-56</c>, <c>experiment-7-start</c>. The prefix names the source.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// What changed.
    /// </summary>
    public ImpactEventKind Kind { get; init; }

    /// <summary>
    /// UTC day the change took effect (see <see cref="DateSource"/>).
    /// </summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// UTC instant the change took effect, when known.
    /// </summary>
    public DateTime? At { get; init; }

    /// <summary>
    /// Where <see cref="Date"/> comes from.
    /// </summary>
    public ImpactDateSource DateSource { get; init; }

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; init; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; init; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; init; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; init; }

    /// <summary>
    /// One line title (the plan title, "1.4 is live").
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Locales the change touched (empty when not localized).
    /// </summary>
    public IReadOnlyList<string> Locales { get; init; } = [];

    /// <summary>
    /// Fields the change touched (subtitle, description, phoneScreenshots...).
    /// </summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    /// <summary>
    /// The change plan, for a plan event.
    /// </summary>
    public long? PlanId { get; init; }

    /// <summary>
    /// User facing version, when the event belongs to one.
    /// </summary>
    public string VersionString { get; init; }

    /// <summary>
    /// Events with the same key went live together (a version and the listing written to it); they are related, not
    /// confounding. Null when the event stands alone.
    /// </summary>
    public string ReleaseKey { get; init; }

    /// <summary>
    /// The source's own id of the record (the experiment id, the Microsoft submission id...), when there is one.
    /// </summary>
    public string ExternalId { get; init; }
}

/// <summary>
/// Settings of a before and after comparison.
/// </summary>
/// <param name="WindowDays">Length of each window in days (7, 14 or 28 usually).</param>
/// <param name="GapDays">Days skipped from the effect day on before the after window starts (2 skips the day and the next).</param>
/// <param name="Country">Only this country (ISO code); null for every country.</param>
/// <param name="Source">Only this traffic source; null for every source.</param>
/// <param name="Today">Today (UTC); the windows are compared with it to tell whether the report is final.</param>
public sealed record ImpactOptions(int WindowDays, int GapDays, string Country, TrafficSource? Source, DateOnly Today)
{
    /// <summary>
    /// Default window length.
    /// </summary>
    public const int DefaultWindowDays = 14;

    /// <summary>
    /// Default gap: the effect day and the day after are skipped (stores apply changes with a delay).
    /// </summary>
    public const int DefaultGapDays = 2;

    /// <summary>
    /// Shortest window.
    /// </summary>
    public const int MinWindowDays = 3;

    /// <summary>
    /// Longest window.
    /// </summary>
    public const int MaxWindowDays = 56;

    /// <summary>
    /// Longest gap.
    /// </summary>
    public const int MaxGapDays = 14;
}
