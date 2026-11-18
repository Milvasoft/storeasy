using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Milvasoft.Caching.Redis.Accessor;
using Storeasy.Application.Dtos.AccountDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IGoogleSignIn"/>. The discovery document and the signing keys are read from
/// <see cref="GoogleAuthOptions.Authority"/> (https only) and cached for <see cref="MetadataLifetime"/>; an unknown key id
/// refreshes them at most once a minute. The state, the code verifier and the nonce are random 32 byte values; the state
/// is the Redis key of the other two, deleted when read (GETDEL), so a state works once. The redirect address is always
/// <c>{Storeasy:App:BaseUrl}/</c><see cref="AppOptions.GoogleCallbackPath"/>. Registered as a singleton.
/// </summary>
/// <param name="cache"></param>
/// <param name="cacheKeys"></param>
/// <param name="httpClientFactory"></param>
/// <param name="options"></param>
/// <param name="appOptions"></param>
/// <param name="timeProvider"></param>
public sealed class GoogleSignInService(IRedisAccessor cache,
                                        CacheKeys cacheKeys,
                                        IHttpClientFactory httpClientFactory,
                                        GoogleAuthOptions options,
                                        AppOptions appOptions,
                                        TimeProvider timeProvider) : IGoogleSignIn
{
    /// <summary>
    /// Name of the HTTP client that talks to Google (tests replace its handler).
    /// </summary>
    public const string HttpClientName = "google-oidc";

    /// <summary>
    /// How long the discovery document and the keys are used.
    /// </summary>
    public static readonly TimeSpan MetadataLifetime = TimeSpan.FromHours(1);

    private static readonly TimeSpan _clockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(1);

    private readonly IRedisAccessor _cache = cache;
    private readonly CacheKeys _cacheKeys = cacheKeys;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly GoogleAuthOptions _options = options;
    private readonly AppOptions _appOptions = appOptions;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };
    private readonly SemaphoreSlim _metadataLock = new(1, 1);
    private GoogleMetadata _metadata;

    /// <inheritdoc/>
    public bool IsEnabled => _options.Enabled;

    private string RedirectUri => $"{(_appOptions.BaseUrl ?? string.Empty).TrimEnd('/')}/{AppOptions.GoogleCallbackPath}";

    /// <inheritdoc/>
    public async Task<GoogleSignInStartDto> StartAsync(CancellationToken cancellationToken)
    {
        if (!_cache.IsConnected())
            return null;

        var metadata = await GetMetadataAsync(null, cancellationToken);
        var state = RandomValue();
        var codeVerifier = RandomValue();
        var nonce = RandomValue();

        try
        {
            var stored = JsonSerializer.Serialize(new PendingSignIn(codeVerifier, nonce));

            await _cache.GetDatabase().StringSetAsync(_cacheKeys.GoogleSignInState(state), stored, TimeSpan.FromMinutes(Math.Max(1, _options.StateLifetimeMinutes)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }

        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var query = string.Join('&', new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = RedirectUri,
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account",
        }.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

        return new GoogleSignInStartDto { AuthorizationUrl = $"{metadata.AuthorizationEndpoint}?{query}", State = state };
    }

    /// <inheritdoc/>
    public async Task<GoogleSignInResult> CompleteAsync(string code, string state, CancellationToken cancellationToken)
    {
        if (!_cache.IsConnected())
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInUnavailable);

        PendingSignIn pending;

        try
        {
            var stored = await _cache.GetDatabase().StringGetDeleteAsync(_cacheKeys.GoogleSignInState(state));

            pending = stored.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PendingSignIn>(stored.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInUnavailable);
        }

        if (pending == null)
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInStateInvalid);

        try
        {
            var metadata = await GetMetadataAsync(null, cancellationToken);
            var idToken = await ExchangeAsync(metadata, code, pending.CodeVerifier, cancellationToken);

            return await ValidateAsync(idToken, pending.Nonce, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException or ArgumentException or SecurityTokenException)
        {
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInFailed);
        }
    }

    private async Task<string> ExchangeAsync(GoogleMetadata metadata, string code, string codeVerifier, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code_verifier"] = codeVerifier,
        });

        using var response = await client.PostAsync(metadata.TokenEndpoint, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The token endpoint answered {(int)response.StatusCode}.");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return body.RootElement.TryGetProperty("id_token", out var idToken) && idToken.ValueKind == JsonValueKind.String
            ? idToken.GetString()
            : throw new InvalidOperationException("The token endpoint returned no id token.");
    }

    private async Task<GoogleSignInResult> ValidateAsync(string idToken, string nonce, CancellationToken cancellationToken)
    {
        var unverified = new JsonWebToken(idToken);
        var metadata = await GetMetadataAsync(unverified.Kid, cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var parameters = new TokenValidationParameters
        {
            ValidIssuers = metadata.Issuers,
            ValidateIssuer = true,
            ValidAudience = _options.ClientId,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = metadata.Keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            LifetimeValidator = (notBefore, expires, _, _) => expires != null && expires.Value.ToUniversalTime() > now - _clockSkew && (notBefore == null || notBefore.Value.ToUniversalTime() <= now + _clockSkew),
        };

        var result = await _handler.ValidateTokenAsync(idToken, parameters);

        if (!result.IsValid)
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInFailed);

        var token = (JsonWebToken)result.SecurityToken;

        if (!token.TryGetPayloadValue<string>("nonce", out var tokenNonce) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(tokenNonce ?? string.Empty), Encoding.UTF8.GetBytes(nonce)))
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInFailed);

        var subject = token.Subject;
        var email = Claim(token, "email");

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            return GoogleSignInResult.Fail(MessageKey.GoogleSignInFailed);

        var verified = string.Equals(Claim(token, "email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        return new GoogleSignInResult(new GoogleIdentity(subject, email.Trim(), verified, Claim(token, "given_name"), Claim(token, "family_name")), null);
    }

    private static string Claim(JsonWebToken token, string type) => token.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private async Task<GoogleMetadata> GetMetadataAsync(string keyId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cached = _metadata;

        if (IsUsable(cached, keyId, now))
            return cached;

        await _metadataLock.WaitAsync(cancellationToken);

        try
        {
            if (IsUsable(_metadata, keyId, now))
                return _metadata;

            _metadata = await FetchMetadataAsync(now, cancellationToken);

            return _metadata;
        }
        finally
        {
            _metadataLock.Release();
        }
    }

    private static bool IsUsable(GoogleMetadata metadata, string keyId, DateTimeOffset now)
    {
        if (metadata == null || now - metadata.FetchDate >= MetadataLifetime)
            return false;

        return keyId == null || metadata.Keys.Any(k => k.KeyId == keyId) || now - metadata.FetchDate < _refreshInterval;
    }

    private async Task<GoogleMetadata> FetchMetadataAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.Authority?.TrimEnd('/') + "/", UriKind.Absolute, out var authority) || authority.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The Google authority must be an https address.");

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var discovery = JsonDocument.Parse(await client.GetStringAsync(new Uri(authority, ".well-known/openid-configuration"), cancellationToken));

        var root = discovery.RootElement;
        var issuer = Required(root, "issuer");
        var keySet = new JsonWebKeySet(await client.GetStringAsync(RequireHttps(Required(root, "jwks_uri")), cancellationToken));

        // Google's id tokens carry either form of its issuer.
        var issuers = new[] { issuer, issuer.Replace("https://", string.Empty, StringComparison.Ordinal) };

        return new GoogleMetadata(RequireHttps(Required(root, "authorization_endpoint")).ToString(),
                                  RequireHttps(Required(root, "token_endpoint")),
                                  issuers,
                                  [.. keySet.GetSigningKeys()],
                                  now);
    }

    private static string Required(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : throw new InvalidOperationException($"The discovery document has no {name}.");

    private static Uri RequireHttps(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : throw new InvalidOperationException("A Google endpoint is not https.");

    private static string RandomValue() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private sealed record PendingSignIn(string CodeVerifier, string Nonce);

    private sealed record GoogleMetadata(string AuthorizationEndpoint, Uri TokenEndpoint, string[] Issuers, IReadOnlyList<SecurityKey> Keys, DateTimeOffset FetchDate);
}
