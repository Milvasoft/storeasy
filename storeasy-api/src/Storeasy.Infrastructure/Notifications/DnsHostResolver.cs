using Storeasy.Application.Interfaces;
using System.Net;

namespace Storeasy.Infrastructure.Notifications;

/// <summary>
/// <see cref="IHostResolver"/> over the system resolver.
/// </summary>
public sealed class DnsHostResolver : IHostResolver
{
    /// <inheritdoc/>
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Dns.GetHostAddressesAsync(host, cancellationToken);
}
