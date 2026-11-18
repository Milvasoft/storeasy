namespace Storeasy.Application.Interfaces;

/// <summary>
/// The rule watcher (docs/urun-plani.md 12.3): reads the configured official sources (allow-listed domains only),
/// hashes their normalized text, and on a change opens a catalog proposal with the diff for the platform admins.
/// </summary>
public interface IRuleWatcher
{
    /// <summary>
    /// Adds the configured sources that are not in the database yet.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task EnsureSourcesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads every active source once and returns what happened.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<RuleWatchResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of one watcher run.
/// </summary>
/// <param name="Checked">Sources read.</param>
/// <param name="Changed">Sources whose content changed.</param>
/// <param name="Unreachable">Sources that could not be read.</param>
/// <param name="ProposalsOpened">Proposals opened.</param>
public sealed record RuleWatchResult(int Checked, int Changed, int Unreachable, int ProposalsOpened);
