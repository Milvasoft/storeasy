using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Storeasy.Application.Utils.Models;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace Storeasy.Infrastructure.Aso;

/// <summary>
/// The one way the ASO sources reach the stores: GET only, a timeout, a User-Agent without personal data, one request at a
/// time per host with a pause between requests, and a memory cache of the answers (bounded, for
/// <see cref="AsoOptions.FreshnessHours"/>). A failure (status, timeout, network) is logged without the URL's query and
/// answers null; it never throws to the caller except for cancellation.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class AsoHttp(IHttpClientFactory httpClientFactory, AsoOptions options, TimeProvider timeProvider, ILogger<AsoHttp> logger) : IDisposable
{
    /// <summary>
    /// Name of the HTTP client (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "Aso";

    /// <summary>
    /// Most answers kept in memory.
    /// </summary>
    public const int CacheEntryLimit = 5000;

    // The stores' pages are text; an answer bigger than this is not what was asked for.
    private const int _maxResponseBytes = 4 * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly AsoOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<AsoHttp> _logger = logger;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = CacheEntryLimit });
    private readonly ConcurrentDictionary<string, HostGate> _gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The body of a GET, or null when the store did not answer with success. Successful answers are cached by URL and
    /// headers.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="headers">Extra request headers (e.g. the App Store storefront).</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> GetStringAsync(Uri url, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        var key = headers == null || headers.Count == 0 ? url.AbsoluteUri : $"{url.AbsoluteUri}|{string.Join('|', headers.OrderBy(h => h.Key).Select(h => $"{h.Key}={h.Value}"))}";

        if (_cache.TryGetValue(key, out string cached))
            return cached;

        var gate = _gates.GetOrAdd(url.Host, _ => new HostGate());

        await gate.Lock.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have read it while this one waited.
            if (_cache.TryGetValue(key, out cached))
                return cached;

            var wait = gate.NextRequest - _timeProvider.GetUtcNow();

            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, _timeProvider, cancellationToken);

            var body = await SendAsync(url, headers, cancellationToken);

            gate.NextRequest = _timeProvider.GetUtcNow().AddMilliseconds(Math.Max(0, _options.MinRequestIntervalMilliseconds));

            if (body != null)
            {
                _cache.Set(key, body, new MemoryCacheEntryOptions
                {
                    Size = 1,
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Math.Max(1, _options.FreshnessHours)),
                });
            }

            return body;
        }
        finally
        {
            gate.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cache.Dispose();

        foreach (var gate in _gates.Values)
            gate.Lock.Dispose();
    }

    private async Task<string> SendAsync(Uri url, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.UserAgent.Clear();
            request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrWhiteSpace(_options.UserAgent) ? AsoOptions.DefaultUserAgent : _options.UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            foreach (var (name, value) in headers ?? new Dictionary<string, string>())
                request.Headers.TryAddWithoutValidation(name, value);

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ASO source {Host}{Path} answered {Status}.", url.Host, url.AbsolutePath, (int)response.StatusCode);

                return null;
            }

            if (response.Content.Headers.ContentLength > _maxResponseBytes)
                return null;

            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            return body.Length > _maxResponseBytes ? null : body;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ASO source {Host}{Path} timed out.", url.Host, url.AbsolutePath);

            return null;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("ASO source {Host}{Path} could not be reached: {Error}.", url.Host, url.AbsolutePath, exception.HttpRequestError);

            return null;
        }
    }

    /// <summary>
    /// One host's queue: one request at a time, and the earliest instant of the next one.
    /// </summary>
    private sealed class HostGate
    {
        /// <summary>
        /// Held while a request to the host is made.
        /// </summary>
        public SemaphoreSlim Lock { get; } = new(1, 1);

        /// <summary>
        /// Earliest instant of the next request.
        /// </summary>
        public DateTimeOffset NextRequest { get; set; }
    }
}
