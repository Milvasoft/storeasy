using System.Net;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Resolves host names (DNS). Separate so the SSRF checks can be tested without a resolver.
/// </summary>
public interface IHostResolver
{
    /// <summary>
    /// Addresses of <paramref name="host"/>; empty when it does not resolve.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
