using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// Caches Microsoft Entra access tokens per connection and scope, in process memory only (singleton). The key is a hash of
/// the tenant, client, secret and scope, so a rotated secret gets a new token and no secret is kept as a key. A token is
/// reused until <see cref="RefreshMargin"/> before it expires; concurrent callers of one key wait for one request.
/// </summary>
public sealed class MicrosoftTokenCache
{
    /// <summary>
    /// How long before its expiry a token is replaced.
    /// </summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> _tokens = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <summary>
    /// Number of cached tokens (tests).
    /// </summary>
    public int Count => _tokens.Count;

    /// <summary>
    /// Cache key of a connection and scope.
    /// </summary>
    /// <param name="credentials"></param>
    /// <param name="scope"></param>
    /// <returns></returns>
    public static string Key(MicrosoftCredentials credentials, string scope)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{credentials.TenantId}|{credentials.ClientId}|{credentials.ClientSecret}|{scope}")));

    /// <summary>
    /// Returns the cached token of <paramref name="key"/>, or asks <paramref name="acquire"/> for a new one.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="now"></param>
    /// <param name="acquire">Gets a new token and its expiry.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<string> GetAsync(string key, DateTimeOffset now, Func<Task<(string Token, DateTimeOffset ExpiresAt)>> acquire, CancellationToken cancellationToken)
    {
        if (_tokens.TryGetValue(key, out var cached) && cached.ExpiresAt - RefreshMargin > now)
            return cached.Token;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            if (_tokens.TryGetValue(key, out cached) && cached.ExpiresAt - RefreshMargin > now)
                return cached.Token;

            var fresh = await acquire();

            _tokens[key] = fresh;

            return fresh.Token;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Forgets the token of <paramref name="key"/> (after the API refused it).
    /// </summary>
    /// <param name="key"></param>
    public void Remove(string key) => _tokens.TryRemove(key, out _);
}
