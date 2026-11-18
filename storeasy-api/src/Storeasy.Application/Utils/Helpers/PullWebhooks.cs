using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Signature checks of the pull webhooks (design 13.19, 13.20): EAS signs the raw body with HMAC-SHA1 in
/// <c>expo-signature: sha1=&lt;hex&gt;</c>; storeasy's own schema signs <c>&lt;timestamp&gt;.&lt;raw body&gt;</c> with
/// HMAC-SHA256 in <c>X-Storeasy-Signature: sha256=&lt;hex&gt;</c> and refuses timestamps older than
/// <see cref="TimestampTolerance"/>. Comparisons are constant time.
/// </summary>
public static class PullWebhooks
{
    /// <summary>
    /// Largest accepted webhook body.
    /// </summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>
    /// Schema name of the generic webhook body.
    /// </summary>
    public const string ArtifactReadySchema = "storeasy.artifact-ready/v1";

    /// <summary>
    /// Oldest (and furthest in the future) timestamp a generic webhook may carry.
    /// </summary>
    public static readonly TimeSpan TimestampTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Associated data of a pull source's sealed secret.
    /// </summary>
    public static string SecretContext(int organizationId, string publicId) => $"ArtifactPullSource:{organizationId}:{publicId}";

    /// <summary>
    /// Whether <paramref name="signature"/> (<c>sha1=hex</c>) is the HMAC-SHA1 of <paramref name="body"/> with <paramref name="secret"/>.
    /// </summary>
    public static bool IsValidEasSignature(string secret, string body, string signature)
        => Matches(signature, "sha1=", HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body ?? string.Empty)));

    /// <summary>
    /// Whether <paramref name="signature"/> (<c>sha256=hex</c>) is the HMAC-SHA256 of <c>timestamp.body</c> and the timestamp is
    /// within <see cref="TimestampTolerance"/> of <paramref name="now"/>.
    /// </summary>
    public static bool IsValidArtifactSignature(string secret, string body, string timestamp, string signature, DateTime now)
    {
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            return false;

        var sent = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(seconds, 0, 253402300799)).UtcDateTime;

        if ((now - sent).Duration() > TimestampTolerance)
            return false;

        return Matches(signature, "sha256=", HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));
    }

    /// <summary>
    /// The signature header value of a generic webhook (for clients and tests).
    /// </summary>
    public static string SignArtifact(string secret, string body, string timestamp)
        => "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    /// <summary>
    /// The <c>expo-signature</c> header value of an EAS webhook (for tests).
    /// </summary>
    public static string SignEas(string secret, string body) => "sha1=" + Convert.ToHexStringLower(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    private static bool Matches(string signature, string prefix, byte[] expected)
    {
        if (string.IsNullOrWhiteSpace(signature) || !signature.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] given;

        try
        {
            given = Convert.FromHexString(signature.Trim()[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}
