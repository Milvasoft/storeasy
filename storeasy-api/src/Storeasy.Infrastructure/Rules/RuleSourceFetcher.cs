using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net;
using System.Text;

namespace Storeasy.Infrastructure.Rules;

/// <summary>
/// Default <see cref="IRuleSourceFetcher"/>: HTTPS only, allow-listed official hosts only (every redirect hop checked
/// again, at most three), at most <see cref="RuleWatcherOptions.MaxBytes"/> read.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
public sealed class RuleSourceFetcher(IHttpClientFactory httpClientFactory, RuleWatcherOptions options) : IRuleSourceFetcher
{
    /// <summary>
    /// Name of the HTTP client of the watcher (no automatic redirects).
    /// </summary>
    public const string HttpClientName = "rule-watcher";

    private const int _maxRedirects = 3;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly RuleWatcherOptions _options = options;

    /// <inheritdoc/>
    public bool IsAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var host = uri.IdnHost.ToLowerInvariant();

        return (_options.AllowedHosts ?? []).Any(allowed => host == allowed.ToLowerInvariant() || host.EndsWith("." + allowed.ToLowerInvariant(), StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var current = url;

        for (var hop = 0; hop <= _maxRedirects; hop++)
        {
            if (!IsAllowed(current))
                throw new InvalidOperationException($"{current} is not on an allowed official domain.");

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.TryAddWithoutValidation("User-Agent", "storeasy-rule-watcher/1.0");

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
            {
                current = new Uri(new Uri(current), response.Headers.Location).ToString();
                continue;
            }

            if (response.StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"{(int)response.StatusCode} from {current}.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[Math.Max(1024, _options.MaxBytes)];
            var read = 0;

            while (read < buffer.Length)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);

                if (count == 0)
                    break;

                read += count;
            }

            return Encoding.UTF8.GetString(buffer, 0, read);
        }

        throw new HttpRequestException($"Too many redirects from {url}.");
    }
}
