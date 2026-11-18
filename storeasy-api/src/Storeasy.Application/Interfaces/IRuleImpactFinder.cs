namespace Storeasy.Application.Interfaces;

/// <summary>
/// The apps (of every organization) that catalog rules apply to, evaluated with the rules catalog
/// (<see cref="IRulesCatalog"/>) and each app's profile, the same way as the compliance timeline. Used by the rule
/// watcher to show and notify the impact of a catalog proposal.
/// </summary>
public interface IRuleImpactFinder
{
    /// <summary>
    /// The apps <paramref name="ruleIds"/> apply to (or may apply to, when the app's data is incomplete).
    /// </summary>
    /// <param name="ruleIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<RuleImpact>> FindAsync(IReadOnlyList<string> ruleIds, CancellationToken cancellationToken);
}

/// <summary>
/// An app a rule applies to.
/// </summary>
/// <param name="OrganizationId">Owning organization.</param>
/// <param name="AppId">storeasy app.</param>
/// <param name="AppName">App name.</param>
/// <param name="RuleId">The rule.</param>
/// <param name="Message">What the rule asks of the app, with the reason it applies.</param>
public sealed record RuleImpact(int OrganizationId, int AppId, string AppName, string RuleId, string Message);
