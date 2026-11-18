using Milvasoft.Caching.Redis.Accessor;
using Milvasoft.Identity.Concrete.Options;
using StackExchange.Redis;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="ITokenRevocationList"/>: one Redis key per revoked token (its SHA-256 hash), expiring with the
/// access token lifetime. Redis failures are swallowed (see the interface).
/// </summary>
/// <param name="cache"></param>
/// <param name="cacheKeys"></param>
/// <param name="identityOptions"></param>
public sealed class TokenRevocationList(IRedisAccessor cache, CacheKeys cacheKeys, MilvaIdentityOptions identityOptions) : ITokenRevocationList
{
    private readonly IRedisAccessor _cache = cache;
    private readonly CacheKeys _cacheKeys = cacheKeys;
    private readonly MilvaIdentityOptions _identityOptions = identityOptions;

    /// <inheritdoc/>
    public async Task RevokeAsync(IReadOnlyCollection<string> accessTokens)
    {
        var tokens = accessTokens?.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList() ?? [];

        if (tokens.Count == 0 || !_cache.IsConnected())
            return;

        // The token lives at most its lifetime plus the validation clock skew.
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, _identityOptions.Token.ExpirationMinute) + 5);

        try
        {
            var database = _cache.GetDatabase();

            await Task.WhenAll(tokens.Select(token => database.StringSetAsync(_cacheKeys.RevokedToken(SecureTokens.Hash(token)), "1", lifetime)));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Redis unavailable: the session is gone already; the token expires on its own.
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsRevokedAsync(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken) || !_cache.IsConnected())
            return false;

        try
        {
            return await _cache.GetDatabase().KeyExistsAsync(_cacheKeys.RevokedToken(SecureTokens.Hash(accessToken)));
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Redis unavailable: the stamp check still ran; a single revoked token works until it expires.
            return false;
        }
    }
}
