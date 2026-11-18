using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;
using System.Net;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// DNS for the tests: named hosts resolve as configured, every other host to a public address.
/// </summary>
public sealed class FakeHostResolver : IHostResolver
{
    /// <summary>Addresses by host.</summary>
    public ConcurrentDictionary<string, IPAddress[]> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        => Task.FromResult(Hosts.TryGetValue(host, out var addresses) ? addresses : [IPAddress.Parse("93.184.216.34")]);
}
