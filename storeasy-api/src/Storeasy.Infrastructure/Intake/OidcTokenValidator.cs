using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Intake;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// Default <see cref="IOidcTokenValidator"/>. Signing keys come from the issuer's discovery document
/// (<c>{issuer}/.well-known/openid-configuration</c>, then its <c>jwks_uri</c>, which must be https on the issuer's host) and
/// are cached for <see cref="KeyCacheLifetime"/>; an unknown key id refreshes them at most once a minute. Only allow-listed
/// issuers are ever contacted, so a token cannot make the server fetch an arbitrary address. Registered as a singleton.
/// </summary>
/// <param name="httpClientFactory"></param>
/// <param name="timeProvider"></param>
public sealed class OidcTokenValidator(IHttpClientFactory httpClientFactory, TimeProvider timeProvider) : IOidcTokenValidator
{
    /// <summary>
    /// Name of the HTTP client that fetches discovery documents and key sets (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "oidc";

    /// <summary>
    /// How long fetched keys are used.
    /// </summary>
    public static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Allowed clock difference for <c>exp</c>, <c>nbf</c> and <c>iat</c>.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);
    private const int _maxTokenLength = 16 * 1024;

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ConcurrentDictionary<string, CachedKeys> _keys = new(StringComparer.Ordinal);
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    /// <inheritdoc/>
    public async Task<OidcValidation> ValidateAsync(string token, IReadOnlyCollection<string> extraIssuers, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > _maxTokenLength || !_handler.CanReadToken(token))
            return OidcValidation.Fail("The token is not a JWT.");

        JsonWebToken unverified;

        try
        {
            unverified = new JsonWebToken(token);
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenMalformedException)
        {
            return OidcValidation.Fail("The token is not a JWT.");
        }

        var issuer = unverified.Issuer?.TrimEnd('/');

        if (!IsAllowedIssuer(issuer, extraIssuers))
            return OidcValidation.Fail("The issuer is not allowed.", issuer);

        IReadOnlyList<SecurityKey> keys;

        try
        {
            keys = await GetKeysAsync(unverified.Issuer, unverified.Kid, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException or ArgumentException)
        {
            return OidcValidation.Fail("The issuer's signing keys could not be read.", issuer);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = unverified.Issuer,
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = keys,
            ClockSkew = ClockSkew,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512, SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384, SecurityAlgorithms.RsaSsaPssSha256],
            LifetimeValidator = (notBefore, expires, _, _) => expires != null && expires.Value.ToUniversalTime() > now - ClockSkew && (notBefore == null || notBefore.Value.ToUniversalTime() <= now + ClockSkew),
        };

        var result = await _handler.ValidateTokenAsync(token, parameters);

        if (!result.IsValid)
            return OidcValidation.Fail(result.Exception is SecurityTokenExpiredException ? "The token has expired." : "The token signature or lifetime is not valid.", issuer);

        var validated = (JsonWebToken)result.SecurityToken;

        if (validated.IssuedAt != DateTime.MinValue && validated.IssuedAt.ToUniversalTime() > now + ClockSkew)
            return OidcValidation.Fail("The token was issued in the future.", issuer);

        var claims = validated.Claims.Where(c => c.Type is not ("aud" or "iss"))
                                     .GroupBy(c => c.Type, StringComparer.Ordinal)
                                     .ToDictionary(g => g.Key, g => string.Join(",", g.Select(c => c.Value)), StringComparer.Ordinal);

        return new OidcValidation(true, issuer, [.. validated.Audiences], claims, null);
    }

    /// <summary>
    /// Whether <paramref name="issuer"/> may be trusted: GitHub, gitlab.com, an Azure DevOps organization issuer, or one of
    /// <paramref name="extraIssuers"/> (https only).
    /// </summary>
    /// <param name="issuer"></param>
    /// <param name="extraIssuers"></param>
    /// <returns></returns>
    public static bool IsAllowedIssuer(string issuer, IReadOnlyCollection<string> extraIssuers)
    {
        if (string.IsNullOrWhiteSpace(issuer) || !Uri.TryCreate(issuer, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.Query))
            return false;

        return issuer == TrustedPublisherMatcher.GitHubIssuer
               || issuer == TrustedPublisherMatcher.GitLabIssuer
               || TrustedPublisherMatcher.IsAzureIssuer(issuer)
               || (extraIssuers?.Any(e => string.Equals(e?.TrimEnd('/'), issuer, StringComparison.Ordinal)) ?? false);
    }

    private async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(string issuer, string keyId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        if (_keys.TryGetValue(issuer, out var cached))
        {
            var fresh = now - cached.FetchDate < KeyCacheLifetime;
            var hasKey = keyId == null || cached.Keys.Any(k => k.KeyId == keyId);

            if (fresh && (hasKey || now - cached.FetchDate < _refreshInterval))
                return cached.Keys;
        }

        var keys = await FetchKeysAsync(issuer, cancellationToken);

        _keys[issuer] = new CachedKeys(keys, now);

        return keys;
    }

    private async Task<IReadOnlyList<SecurityKey>> FetchKeysAsync(string issuer, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var issuerUri = new Uri(issuer.TrimEnd('/') + "/");

        using var discovery = JsonDocument.Parse(await client.GetStringAsync(new Uri(issuerUri, ".well-known/openid-configuration"), cancellationToken));

        var jwksUri = discovery.RootElement.TryGetProperty("jwks_uri", out var value) ? value.GetString() : null;

        if (!Uri.TryCreate(jwksUri, UriKind.Absolute, out var jwks) || jwks.Scheme != Uri.UriSchemeHttps || !string.Equals(jwks.Host, issuerUri.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The discovery document names no https key set on the issuer's host.");

        var set = new JsonWebKeySet(await client.GetStringAsync(jwks, cancellationToken));

        return [.. set.GetSigningKeys()];
    }

    private sealed record CachedKeys(IReadOnlyList<SecurityKey> Keys, DateTimeOffset FetchDate);
}
