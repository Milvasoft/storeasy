using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Random bearer secrets (invitation tokens, personal access tokens) and their stored form. Only the hash is persisted;
/// lookups hash the presented value and compare in the database.
/// </summary>
public static class SecureTokens
{
    /// <summary>
    /// Creates a URL safe random token with <paramref name="prefix"/> (e.g. "inv_") and 32 random bytes.
    /// </summary>
    public static string Create(string prefix)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Lower case hex SHA-256 of <paramref name="token"/>.
    /// </summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}
