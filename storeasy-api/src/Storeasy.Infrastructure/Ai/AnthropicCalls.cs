using Anthropic;
using Anthropic.Exceptions;

namespace Storeasy.Infrastructure.Ai;

/// <summary>
/// What every AI feature shares about calling Anthropic with an organization's key: the client (on the named HTTP client,
/// so tests and proxies see every call) and how SDK exceptions are classified. The key lives in the client for one call
/// only and is never logged.
/// </summary>
public static class AnthropicCalls
{
    /// <summary>
    /// Name of the HTTP client the SDK sends with.
    /// </summary>
    public const string HttpClientName = "anthropic";

    /// <summary>
    /// A client for one call with <paramref name="apiKey"/>.
    /// </summary>
    /// <param name="httpClientFactory"></param>
    /// <param name="apiKey"></param>
    /// <param name="timeoutSeconds"></param>
    /// <param name="maxRetries">Retries of a 429 or 5xx (the SDK honours Retry-After).</param>
    /// <returns></returns>
    public static AnthropicClient CreateClient(IHttpClientFactory httpClientFactory, string apiKey, int timeoutSeconds, int maxRetries) => new()
    {
        ApiKey = apiKey,
        HttpClient = httpClientFactory.CreateClient(HttpClientName),
        MaxRetries = maxRetries,
        Timeout = TimeSpan.FromSeconds(timeoutSeconds),
    };

    /// <summary>
    /// Failure class of an exception thrown by the SDK: typed exceptions first, the status code of any other API error next.
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static AiCallFailure Classify(Exception exception) => exception switch
    {
        AnthropicUnauthorizedException or AnthropicForbiddenException => AiCallFailure.KeyRejected,
        AnthropicRateLimitException => AiCallFailure.RateLimited,
        Anthropic5xxException or AnthropicIOException or HttpRequestException or TimeoutException or TaskCanceledException => AiCallFailure.Unavailable,
        AnthropicApiException { StatusCode: var status } when (int)status >= 500 => AiCallFailure.Unavailable,
        _ => AiCallFailure.Failed,
    };
}

/// <summary>
/// Why a call to Anthropic failed.
/// </summary>
public enum AiCallFailure
{
    /// <summary>
    /// The provider rejected the key (401 or 403).
    /// </summary>
    KeyRejected = 1,

    /// <summary>
    /// The provider's rate limit was reached (429).
    /// </summary>
    RateLimited = 2,

    /// <summary>
    /// The provider is overloaded, failing or unreachable (5xx, 529, network, timeout).
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// Anything else (a request the provider refused).
    /// </summary>
    Failed = 4,
}
