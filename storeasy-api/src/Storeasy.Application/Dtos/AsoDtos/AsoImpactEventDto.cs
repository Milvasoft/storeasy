namespace Storeasy.Application.Dtos.AsoDtos;

/// <summary>
/// A change that may have moved the store page metrics (see <see cref="ImpactEvent"/>).
/// </summary>
public class AsoImpactEventDto
{
    /// <summary>
    /// Stable id (<c>plan-12</c>, <c>version-34</c>, <c>ms-56</c>); pass it to the impact report.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// What changed.
    /// </summary>
    public ImpactEventKind Kind { get; set; }

    /// <summary>
    /// UTC day the change took effect.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// UTC instant the change took effect, when known.
    /// </summary>
    public DateTime? At { get; set; }

    /// <summary>
    /// Where <see cref="Date"/> comes from (live in the store, applied, awaiting release).
    /// </summary>
    public ImpactDateSource DateSource { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name of the app (organization lists).
    /// </summary>
    public string AppName { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// One line title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Locales the change touched.
    /// </summary>
    public List<string> Locales { get; set; } = [];

    /// <summary>
    /// Fields the change touched.
    /// </summary>
    public List<string> Fields { get; set; } = [];

    /// <summary>
    /// The change plan, for a plan event.
    /// </summary>
    public long? PlanId { get; set; }

    /// <summary>
    /// User facing version, when the event belongs to one.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// The source's own id of the record (Microsoft submission id, experiment id), when there is one.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// Maps an event.
    /// </summary>
    /// <param name="impactEvent"></param>
    /// <param name="appName"></param>
    /// <returns></returns>
    public static AsoImpactEventDto From(ImpactEvent impactEvent, string appName = null) => new()
    {
        Id = impactEvent.Id,
        Kind = impactEvent.Kind,
        Date = impactEvent.Date,
        At = impactEvent.At,
        DateSource = impactEvent.DateSource,
        AppId = impactEvent.AppId,
        AppName = appName,
        StoreAppId = impactEvent.StoreAppId,
        Store = impactEvent.Store,
        Title = impactEvent.Title,
        Locales = [.. impactEvent.Locales],
        Fields = [.. impactEvent.Fields],
        PlanId = impactEvent.PlanId,
        VersionString = impactEvent.VersionString,
        ExternalId = impactEvent.ExternalId,
    };
}
