using Microsoft.Extensions.Logging;
using Milvasoft.Caching.Redis.Accessor;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Holds the first run setup code of a single team instance for the life of the process (singleton). The generated
/// code is also put into Redis with "set if missing", so every instance that starts while the database has no user
/// shares the first instance's code; this instance's own copy is the fallback while Redis is unreachable. A configured
/// <see cref="BootstrapOptions.SetupCode"/> replaces both and is never written to the log.
/// </summary>
/// <param name="cache"></param>
/// <param name="cacheKeys"></param>
/// <param name="bootstrapOptions"></param>
/// <param name="logger"></param>
public sealed class SetupCodeStore(IRedisAccessor cache, CacheKeys cacheKeys, BootstrapOptions bootstrapOptions, ILogger<SetupCodeStore> logger)
{
    /// <summary>
    /// Letters and digits of a generated code: no 0/O and 1/I/L, so a code copied by hand from a log is not misread.
    /// </summary>
    private const string _alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int _groupLength = 4;
    private const int _groupCount = 3;

    private readonly IRedisAccessor _cache = cache;
    private readonly CacheKeys _cacheKeys = cacheKeys;
    private readonly BootstrapOptions _bootstrapOptions = bootstrapOptions;
    private readonly ILogger<SetupCodeStore> _logger = logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile string _localCode;
    private volatile bool _completed;

    /// <summary>
    /// True once a user is known to exist: the setup never runs again in this process.
    /// </summary>
    public bool Completed => _completed;

    /// <summary>
    /// Generates the code (or takes the configured one) and writes it to the log once. Does nothing when a code is
    /// already held or the setup was completed.
    /// </summary>
    /// <returns></returns>
    public async Task PrepareAsync()
    {
        if (_completed || _localCode != null)
            return;

        await _gate.WaitAsync();

        try
        {
            if (_completed || _localCode != null)
                return;

            if (_bootstrapOptions.IsSetupCodeConfigured)
            {
                _localCode = _bootstrapOptions.SetupCode.Trim();

                _logger.LogWarning("Storeasy is not set up yet. Open the web app and enter the setup code from Storeasy:Bootstrap:SetupCode.");

                return;
            }

            var code = Generate();

            code = await ShareAsync(code);

            _localCode = code;

            // Written once, at Warning so it passes every log level filter; :l writes the code without quotes.
            _logger.LogWarning("Storeasy is not set up yet. Open the web app and enter the one time setup code below to create the administrator and the team.");
            _logger.LogWarning("Storeasy setup code: {SetupCode:l}", code);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Compares <paramref name="code"/> with the configured code, or with the shared and the local generated code, in
    /// constant time. Case, spaces and dashes are ignored.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public async Task<bool> VerifyAsync(string code)
    {
        if (_completed || string.IsNullOrWhiteSpace(code))
            return false;

        if (_bootstrapOptions.IsSetupCodeConfigured)
            return Matches(code, _bootstrapOptions.SetupCode);

        var shared = await ReadSharedAsync();

        // Both are compared (no early exit), so the answer takes the same time whichever one matches.
        var matchesShared = shared != null && Matches(code, shared);
        var matchesLocal = _localCode != null && Matches(code, _localCode);

        return matchesShared | matchesLocal;
    }

    /// <summary>
    /// Forgets the code (here and in Redis) for good: a user exists.
    /// </summary>
    /// <returns></returns>
    public async Task CompleteAsync()
    {
        _completed = true;
        _localCode = null;

        if (!_cache.IsConnected())
            return;

        try
        {
            await _cache.GetDatabase().KeyDeleteAsync(_cacheKeys.SetupCode());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The setup code could not be removed from Redis.");
        }
    }

    /// <summary>
    /// Marks the setup as completed without touching Redis (a user was found in the database).
    /// </summary>
    public void MarkCompleted()
    {
        _completed = true;
        _localCode = null;
    }

    /// <summary>
    /// Normalizes a code for the comparison: upper case, without spaces and dashes.
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static string Normalize(string code)
    {
        if (string.IsNullOrEmpty(code))
            return string.Empty;

        var builder = new StringBuilder(code.Length);

        foreach (var c in code)
        {
            if (c != '-' && !char.IsWhiteSpace(c))
                builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A new random code, <c>XXXX-XXXX-XXXX</c>.
    /// </summary>
    /// <returns></returns>
    public static string Generate()
    {
        var characters = RandomNumberGenerator.GetItems<char>(_alphabet, _groupLength * _groupCount);

        return string.Join('-', Enumerable.Range(0, _groupCount).Select(group => new string(characters, group * _groupLength, _groupLength)));
    }

    private static bool Matches(string provided, string expected)
        => CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(provided))), SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(expected))));

    private async Task<string> ShareAsync(string code)
    {
        if (!_cache.IsConnected())
            return code;

        try
        {
            var database = _cache.GetDatabase();
            var key = _cacheKeys.SetupCode();

            await database.StringSetAsync(key, code, when: StackExchange.Redis.When.NotExists);

            var shared = await database.StringGetAsync(key);

            return shared.HasValue ? shared.ToString() : code;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The setup code could not be shared through Redis; this instance keeps its own.");

            return code;
        }
    }

    private async Task<string> ReadSharedAsync()
    {
        if (!_cache.IsConnected())
            return null;

        try
        {
            var shared = await _cache.GetDatabase().StringGetAsync(_cacheKeys.SetupCode());

            return shared.HasValue ? shared.ToString() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
