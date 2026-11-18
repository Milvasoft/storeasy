using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// HMAC-SHA256 webhook signatures. App Store Connect signs the raw body and sends <c>x-apple-signature: hmacsha256=&lt;hex&gt;</c>;
/// storeasy's own scheme (inbound signed webhooks and outgoing deliveries) signs <c>{timestamp}.{body}</c> and sends
/// <c>X-Storeasy-Signature: sha256=&lt;hex&gt;</c> with <c>X-Storeasy-Timestamp</c> (unix seconds). Comparisons are constant time.
/// </summary>
public static class WebhookSignatures
{
    /// <summary>
    /// Header App Store Connect signs with.
    /// </summary>
    public const string AppleHeader = "x-apple-signature";

    /// <summary>
    /// Signature header of storeasy's scheme.
    /// </summary>
    public const string SignatureHeader = "X-Storeasy-Signature";

    /// <summary>
    /// Timestamp header of storeasy's scheme.
    /// </summary>
    public const string TimestampHeader = "X-Storeasy-Timestamp";

    /// <summary>
    /// Event id header of storeasy's scheme.
    /// </summary>
    public const string EventIdHeader = "X-Storeasy-Event-Id";

    /// <summary>
    /// Event type header of outgoing deliveries.
    /// </summary>
    public const string EventTypeHeader = "X-Storeasy-Event";

    private const string _applePrefix = "hmacsha256=";
    private const string _storeasyPrefix = "sha256=";
    private const int _hexLength = 64;

    /// <summary>
    /// The App Store Connect header value for <paramref name="body"/>.
    /// </summary>
    public static string ComputeApple(string body, string secret) => _applePrefix + Hex(secret, body);

    /// <summary>
    /// Whether <paramref name="header"/> is a valid App Store Connect signature of <paramref name="body"/>.
    /// </summary>
    public static bool VerifyApple(string body, string header, string secret) => Matches(body, Strip(header, _applePrefix), secret);

    /// <summary>
    /// The storeasy signature header value of <paramref name="body"/> sent at <paramref name="timestamp"/>.
    /// </summary>
    public static string ComputeStoreasy(long timestamp, string body, string secret) => _storeasyPrefix + Hex(secret, Signed(timestamp, body));

    /// <summary>
    /// Whether <paramref name="header"/> is a valid storeasy signature of <paramref name="body"/> sent at <paramref name="timestamp"/>.
    /// </summary>
    public static bool VerifyStoreasy(long timestamp, string body, string header, string secret) => Matches(Signed(timestamp, body), Strip(header, _storeasyPrefix), secret);

    /// <summary>
    /// The header with every hex character but the first and last four masked, for display and logs.
    /// </summary>
    public static string Mask(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        var value = header.Trim();
        var separator = value.IndexOf('=');
        var prefix = separator >= 0 ? value[..(separator + 1)] : string.Empty;
        var hex = separator >= 0 ? value[(separator + 1)..] : value;

        if (hex.Length <= 8)
            return prefix + new string('*', hex.Length);

        return $"{prefix}{hex[..4]}{new string('*', Math.Min(hex.Length - 8, 16))}{hex[^4..]}";
    }

    /// <summary>
    /// Lower case hex SHA-256 of <paramref name="text"/>.
    /// </summary>
    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    private static string Signed(long timestamp, string body) => $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{body}";

    private static string Hex(string secret, string text) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret ?? string.Empty), Encoding.UTF8.GetBytes(text ?? string.Empty)));

    private static string Strip(string header, string prefix)
    {
        var value = header?.Trim();

        if (string.IsNullOrEmpty(value))
            return null;

        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..] : value;
    }

    private static bool Matches(string text, string givenHex, string secret)
    {
        if (string.IsNullOrEmpty(secret) || givenHex == null || givenHex.Length != _hexLength)
            return false;

        byte[] given;

        try
        {
            given = Convert.FromHexString(givenHex);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text ?? string.Empty));

        return CryptographicOperations.FixedTimeEquals(expected, given);
    }
}
