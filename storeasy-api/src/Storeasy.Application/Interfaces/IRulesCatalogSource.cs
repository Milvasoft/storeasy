namespace Storeasy.Application.Interfaces;

/// <summary>
/// What phase 4 needs from the store rules catalog (docs/urun-plani.md 12): the deadlines that apply to apps (compliance
/// timeline notifications), the apps a rule change affects (watcher approvals) and publishing an approved change.
/// The catalog loader of phase 2 (<c>IRulesCatalog</c>) implements it; until it is wired the API registers a source that
/// knows no rules, so the compliance notifier stays silent and an approval notifies nobody.
/// </summary>
public interface IRulesCatalogSource
{
    /// <summary>
    /// Version of the loaded catalog (<c>catalogVersion</c> of <c>rules/catalog.json</c>); null when none is loaded.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> GetCatalogVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The rules with a deadline (or a <c>required-from</c> date) between <paramref name="from"/> and <paramref name="to"/>
    /// that apply to each of <paramref name="apps"/>, matched with the app's data (device families, build SDK, target API,
    /// regions, features). One entry per app and rule.
    /// </summary>
    /// <param name="apps"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<CatalogDeadline>> GetDeadlinesAsync(IReadOnlyList<CatalogAppContext> apps, DateTime from, DateTime to, CancellationToken cancellationToken);

    /// <summary>
    /// The apps (of every organization) that <paramref name="ruleIds"/> apply to, with a one line reason each.
    /// </summary>
    /// <param name="ruleIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<CatalogAffectedApp>> FindAffectedAppsAsync(IReadOnlyList<string> ruleIds, CancellationToken cancellationToken);

    /// <summary>
    /// Publishes an approved catalog change and returns the new catalog version, or null when the catalog cannot be
    /// changed at run time (file based catalog: the patch goes into the repository by a pull request).
    /// </summary>
    /// <param name="change"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> ApplyChangeAsync(CatalogChange change, CancellationToken cancellationToken);
}

/// <summary>
/// An app the catalog is asked about. The catalog reads anything else it needs (device families, SDK, regions) by id.
/// </summary>
/// <param name="AppId">storeasy app.</param>
/// <param name="OrganizationId">Owning organization.</param>
/// <param name="Name">App name.</param>
/// <param name="Stores">Stores the app has records in.</param>
public sealed record CatalogAppContext(int AppId, int OrganizationId, string Name, IReadOnlyList<StoreKind> Stores);

/// <summary>
/// A deadline of one rule for one app.
/// </summary>
/// <param name="AppId">storeasy app.</param>
/// <param name="RuleId">Catalog rule id (<c>apple.age-rating.shared-location-dating</c>).</param>
/// <param name="Store">Store of the rule.</param>
/// <param name="Title">Rule title in the current culture.</param>
/// <param name="DueDate">UTC date the rule starts to apply or must be met.</param>
/// <param name="SourceUrl">Official source.</param>
/// <param name="MatchReason">Why the rule applies to the app.</param>
public sealed record CatalogDeadline(int AppId, string RuleId, StoreKind Store, string Title, DateTime DueDate, string SourceUrl, string MatchReason);

/// <summary>
/// An app a rule change affects.
/// </summary>
/// <param name="OrganizationId">Owning organization.</param>
/// <param name="AppId">storeasy app.</param>
/// <param name="AppName">App name.</param>
/// <param name="RuleId">The rule.</param>
/// <param name="Message">What changes for the app (e.g. "asks for iPhone Duo screenshots once built with the iOS 27.1 SDK").</param>
public sealed record CatalogAffectedApp(int OrganizationId, int AppId, string AppName, string RuleId, string Message);

/// <summary>
/// An approved catalog change.
/// </summary>
/// <param name="ProposalId">The proposal.</param>
/// <param name="RuleIds">Rules it changes.</param>
/// <param name="Patch">The change (JSON) as prepared.</param>
/// <param name="ApprovedByUserName">Approver.</param>
public sealed record CatalogChange(int ProposalId, IReadOnlyList<string> RuleIds, string Patch, string ApprovedByUserName);
