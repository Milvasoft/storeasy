using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// HTTP transport of the content engine with the stores' rate limits in mind: a call answered with 429 or 5xx (or one that
/// did not reach the store) is retried with exponential backoff, honouring <c>Retry-After</c>, up to
/// <see cref="StoreApiOptions.MaxRetries"/> times. Calls that create something are retried only when the caller says a
/// retry is safe. Failures become <see cref="StoreCallException"/> with a redacted message (no tokens, no signed URLs).
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class StoreHttp(IHttpClientFactory httpClientFactory, StoreApiOptions options, TimeProvider timeProvider)
{
    private const int _maxErrorLength = 400;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly StoreApiOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Options of the engine.
    /// </summary>
    public StoreApiOptions Options => _options;

    /// <summary>
    /// Clock of the engine.
    /// </summary>
    public TimeProvider Clock => _timeProvider;

    /// <summary>
    /// Sends a JSON request (body serialized when not null) and parses the JSON answer; an empty answer parses as <c>{}</c>.
    /// </summary>
    /// <param name="method"></param>
    /// <param name="url"></param>
    /// <param name="bearerToken"></param>
    /// <param name="body"></param>
    /// <param name="retry">Whether a 429/5xx answer is retried (default: every method but POST).</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<JsonDocument> SendJsonAsync(HttpMethod method, string url, string bearerToken, object body, bool? retry, CancellationToken cancellationToken)
    {
        var json = body == null ? null : body as string ?? JsonSerializer.Serialize(body);

        var response = await SendAsync(() =>
        {
            var request = new HttpRequestMessage(method, url);

            if (bearerToken != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            if (json != null)
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return request;
        }, retry ?? method != HttpMethod.Post, cancellationToken);

        return Parse(response);
    }

    /// <summary>
    /// Sends the request <paramref name="build"/> creates (once per attempt) and returns the answer; throws
    /// <see cref="StoreCallException"/> for a non success status.
    /// </summary>
    /// <param name="build"></param>
    /// <param name="retry"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="acceptStatus">An extra status that counts as success (e.g. 308 of a resumable upload).</param>
    /// <returns></returns>
    public async Task<StoreHttpResponse> SendAsync(Func<HttpRequestMessage> build, bool retry, CancellationToken cancellationToken, int? acceptStatus = null)
    {
        var client = _httpClientFactory.CreateClient(StoreGateway.HttpClientName);
        var attempts = retry ? Math.Max(0, _options.MaxRetries) + 1 : 1;

        for (var attempt = 1; ; attempt++)
        {
            using var request = build();
            var target = $"{request.Method} {StripQuery(request.RequestUri)}";

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                if (attempt < attempts)
                {
                    await DelayAsync(attempt, null, cancellationToken);
                    continue;
                }

                throw new StoreCallException(SecretRedactor.RedactAndTrim($"{target}: store unreachable ({ex.Message})", _maxErrorLength), null, true);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                var text = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode || status == acceptStatus)
                    return new StoreHttpResponse(status, text, response.Headers.Location?.ToString() ?? HeaderValue(response, "Location"), HeaderValue(response, "Range"));

                var transient = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;

                if (transient && attempt < attempts)
                {
                    await DelayAsync(attempt, response.Headers.RetryAfter, cancellationToken);
                    continue;
                }

                throw new StoreCallException(SecretRedactor.RedactAndTrim($"{target} ({status}): {StoreGateway.DescribeError(text)}", _maxErrorLength), status, transient);
            }
        }
    }

    /// <summary>
    /// Backoff before retry <paramref name="attempt"/>: the store's <c>Retry-After</c> when given, otherwise the base delay
    /// doubled per attempt, never longer than the configured maximum.
    /// </summary>
    /// <param name="attempt"></param>
    /// <param name="retryAfter"></param>
    /// <param name="baseDelay"></param>
    /// <param name="maxDelay"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static TimeSpan RetryDelay(int attempt, RetryConditionHeaderValue retryAfter, TimeSpan baseDelay, TimeSpan maxDelay, DateTimeOffset now)
    {
        TimeSpan delay;

        if (retryAfter?.Delta is { } delta)
            delay = delta;
        else if (retryAfter?.Date is { } date)
            delay = date - now;
        else
            delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;

        return delay > maxDelay ? maxDelay : delay;
    }

    /// <summary>
    /// Parses a JSON answer; an empty answer parses as <c>{}</c>.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    public static JsonDocument Parse(StoreHttpResponse response)
        => JsonDocument.Parse(string.IsNullOrWhiteSpace(response.Body) ? "{}" : response.Body);

    private Task DelayAsync(int attempt, RetryConditionHeaderValue retryAfter, CancellationToken cancellationToken)
    {
        var delay = RetryDelay(attempt,
                               retryAfter,
                               TimeSpan.FromMilliseconds(_options.RetryBaseDelayMilliseconds),
                               TimeSpan.FromSeconds(_options.RetryMaxDelaySeconds),
                               _timeProvider.GetUtcNow());

        return delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, _timeProvider, cancellationToken);
    }

    private static string HeaderValue(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static string StripQuery(Uri uri)
    {
        if (uri == null)
            return string.Empty;

        var text = uri.ToString();
        var index = text.IndexOf('?');

        return index < 0 ? text : text[..index];
    }
}

/// <summary>
/// A store answer.
/// </summary>
/// <param name="Status">HTTP status.</param>
/// <param name="Body">Body text.</param>
/// <param name="Location"><c>Location</c> header (resumable upload session).</param>
/// <param name="Range"><c>Range</c> header (resumable upload progress).</param>
public sealed record StoreHttpResponse(int Status, string Body, string Location, string Range);
