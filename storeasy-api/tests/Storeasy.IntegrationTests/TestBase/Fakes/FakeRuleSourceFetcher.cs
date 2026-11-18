using Storeasy.Application.Interfaces;
using System.Collections.Concurrent;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// Official pages as the tests define them; an unknown page is unreachable.
/// </summary>
public sealed class FakeRuleSourceFetcher : IRuleSourceFetcher
{
    /// <summary>Content by address.</summary>
    public ConcurrentDictionary<string, string> Pages { get; } = new();

    /// <inheritdoc/>
    public bool IsAllowed(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && (uri.Host == "developer.apple.com" || uri.Host == "support.google.com");

    /// <inheritdoc/>
    public Task<string> FetchAsync(string url, CancellationToken cancellationToken)
        => Pages.TryGetValue(url, out var page) ? Task.FromResult(page) : throw new HttpRequestException($"404 from {url}.");
}
