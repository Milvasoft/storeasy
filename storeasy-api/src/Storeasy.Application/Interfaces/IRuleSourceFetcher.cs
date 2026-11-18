namespace Storeasy.Application.Interfaces;

/// <summary>
/// Reads an official source page for the rule watcher. Only HTTPS addresses on the allow-listed official domains are
/// fetched (redirects included); anything else throws <see cref="InvalidOperationException"/>.
/// </summary>
public interface IRuleSourceFetcher
{
    /// <summary>
    /// Returns the body of <paramref name="url"/> as text.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> FetchAsync(string url, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="url"/> is on an allow-listed official domain.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    bool IsAllowed(string url);
}
