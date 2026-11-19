using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Text.Json;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Default <see cref="IGooglePushTokenValidator"/>. The keys come from <see cref="WebhookOptions.GooglePushKeysUrl"/>
/// (https only) and are cached for <see cref="KeyCacheLifetime"/>; an unknown key id refreshes them at most once a minute.
/// Registered as a singleton.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class GooglePushTokenValidator(IHttpClientFactory httpClientFactory, WebhookOptions options, TimeProvider timeProvider) : IGooglePushTokenValidator
{
    /// <summary>
    /// Name of the HTTP client that reads Google's key set (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "google-push";

    /// <summary>
    /// How long fetched keys are used.
    /// </summary>
    public static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromHours(1);

    private static readonly TimeSpan _clockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);
    private const string _bearerPrefix = "Bearer ";
    private const int _maxTokenLength = 16 * 1024;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly WebhookOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<SecurityKey> _keys;
    private DateTimeOffset _fetchDate;

    /// <inheritdoc/>
    public async Task<bool> ValidateAsync(string authorization, string audience, string serviceAccount, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorization) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(serviceAccount)
            || !authorization.StartsWith(_bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var token = authorization[_bearerPrefix.Length..].Trim();

        if (token.Length > _maxTokenLength || !_handler.CanReadToken(token))
            return false;

        try
        {
            var keys = await GetKeysAsync(new JsonWebToken(token).Kid, cancellationToken);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var parameters = new TokenValidationParameters
            {
                ValidIssuers = _options.GooglePushIssuers,
                ValidateIssuer = true,
                ValidAudience = audience,
                ValidateAudience = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                IssuerSigningKeys = keys,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                LifetimeValidator = (notBefore, expires, _, _) => expires != null && expires.Value.ToUniversalTime() > now - _clockSkew && (notBefore == null || notBefore.Value.ToUniversalTime() <= now + _clockSkew),
            };

            var result = await _handler.ValidateTokenAsync(token, parameters);

            if (!result.IsValid)
                return false;

            var validated = (JsonWebToken)result.SecurityToken;

            return validated.TryGetPayloadValue<string>("email", out var email)
                   && string.Equals(email, serviceAccount.Trim(), StringComparison.OrdinalIgnoreCase)
                   && EmailVerified(validated);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException or ArgumentException or SecurityTokenException)
        {
            return false;
        }
    }

    private static bool EmailVerified(JsonWebToken token)
    {
        if (token.TryGetPayloadValue<bool>("email_verified", out var verified))
            return verified;

        return token.TryGetPayloadValue<string>("email_verified", out var text) && string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(string keyId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (IsUsable(keyId, now))
            return _keys;

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (IsUsable(keyId, now))
                return _keys;

            if (!Uri.TryCreate(_options.GooglePushKeysUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Storeasy:Webhooks:GooglePushKeysUrl must be an https address.");

            var set = new JsonWebKeySet(await _httpClientFactory.CreateClient(HttpClientName).GetStringAsync(url, cancellationToken));

            _keys = [.. set.GetSigningKeys()];
            _fetchDate = now;

            return _keys;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsUsable(string keyId, DateTimeOffset now)
    {
        if (_keys == null || now - _fetchDate >= KeyCacheLifetime)
            return false;

        return keyId == null || _keys.Any(k => k.KeyId == keyId) || now - _fetchDate < _refreshInterval;
    }
}
